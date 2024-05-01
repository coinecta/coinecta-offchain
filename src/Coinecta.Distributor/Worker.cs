using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Derivations;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Utilities;
using Coinecta.Data.Utils;
using Cardano.Sync;
using System.Diagnostics;

namespace Coinecta.Distributor;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private HttpClient SubmitApi => httpClientFactory.CreateClient("SubmitApi");
    private List<Utxo> Utxos { get; set; } = [];
    private string Address { get; set; } = "";
    private readonly int _maxTxSize = 16384;

    private readonly string _distributionFile = "distribution.csv";
    private readonly string _processingFile = "processing.csv";
    private readonly string _processedFile = "processed";
    private readonly string _unprocessedFile = "unprocessed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        Stopwatch sw = new();
        sw.Start();

        MnemonicService mnemonicService = new();
        Mnemonic mnemonic = mnemonicService.Restore(configuration["Mnemonic"]!);
        PrivateKey rootKey = mnemonic.GetRootKey();

        // Derive down to our Account Node
        IAccountNodeDerivation accountNode = rootKey.Derive()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        IIndexNodeDerivation paymentNode = accountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        IIndexNodeDerivation stakeNode = accountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        // Set Catcher States
        CardanoSharp.Wallet.Models.Addresses.Address addr = AddressUtility
            .GetBaseAddress(paymentNode.PublicKey, stakeNode.PublicKey, CoinectaUtils.GetNetworkType(configuration));
        PublicKey pubKey = paymentNode.PublicKey;
        PrivateKey privKey = paymentNode.PrivateKey;
        string submitUrl = configuration["CardanoSubmitApiUrl"]!;

        Address = addr.ToString();
        await UpdateUtxosAsync();

        // Check if there are pending distribution file
        bool distributionFileExists = File.Exists(_distributionFile);
        bool processingFileExists = File.Exists(_processingFile);
        string? unprocessedHeader = null;
        string? processedHeader = null;
        // If distribution file exists, rename it to processed.csv
        // Add a column in the first column of the processed.csv file 
        // for the transaction hash using File class
        if (distributionFileExists)
        {
            _logger.LogInformation("Distribution file found.");
            if (processingFileExists)
            {
                File.Delete(_processingFile);
            }

            _logger.LogInformation($"Renaming distribution.csv to {_processingFile}");
            File.Move(_distributionFile, _processingFile);

            // Add empty transaction hash to the processed.csv file for each line
            string[] processingEntries = File.ReadAllLines(_processingFile);
            unprocessedHeader = processingEntries[0];
            List<string> processingBody = processingEntries.Skip(1).Select(l => l + ",").ToList();

            processedHeader = unprocessedHeader + ",transaction_hash";

            await File.WriteAllTextAsync(_processingFile, processedHeader + Environment.NewLine, stoppingToken);
            await File.AppendAllLinesAsync(_processingFile, processingBody, stoppingToken);

        }

        string[] headers = File.ReadAllLines(_processingFile)
            .First()
            .Split(',');
        List<(int i, string e)> entriesToProcess = File.ReadAllLines(_processingFile)
            .Skip(1)
            .Where(l => l.Split(',').Last() == "")
            .Select((e, i) => (i + 1, e))
            .ToList();
        Stack<(int, string)> distributionQueue = new(entriesToProcess);

        List<OutputData> currentOutputData = [];
        List<int> currentOutputDataIndices = [];

        int totalEntries = distributionQueue.Count;

        _logger.LogInformation("Processing {totalEntries} entries in the distribution file.", totalEntries);
        while (distributionQueue.TryPop(out (int, string) line))
        {
            // Build transaction and fill until we reach the max number of inputs
            string[] entry = line.Item2.Split(',');
            string address = entry[0];
            string lovelaceString = entry[1];
            ulong lovelace = string.IsNullOrEmpty(lovelaceString) ? 0 : ulong.Parse(lovelaceString);

            // Assets are the column names starting from the 3rd column, and the cells contain the amount
            // Except for the last column which is the transaction hash
            // The result should be "column_name": "amount" list
            IEnumerable<string> units = entry.Skip(2);
            units = units.Take(units.Count() - 1);

            List<Asset?> assets = units.Select((u, i) =>
            {
                string unit = headers[i + 2];
                string policyId = unit[..56];
                string assetName = unit[56..];

                if (string.IsNullOrEmpty(u)) return null;

                return new Asset()
                {
                    PolicyId = policyId,
                    Name = assetName,
                    Quantity = long.Parse(u)
                };
            })
            .Where(a => a != null)
            .ToList();

            OutputData outputData = new(address, lovelace, assets!);
            currentOutputData.Add(outputData);
            currentOutputDataIndices.Add(line.Item1);

            var (tx, txHash, consumedUtxos, changeUtxos) = Utils.BuildTx(addr.ToString(), pubKey, privKey, currentOutputData, Utxos!);

            // Check if the transaction is too big
            if (tx.Length > _maxTxSize)
            {
                // Remove the last output and try again
                currentOutputData.RemoveAt(currentOutputData.Count - 1);

                // Return the last output to the queue
                distributionQueue.Push(line);

                // Rebuild the transaction
                (tx, txHash, consumedUtxos, changeUtxos) = Utils.BuildTx(addr.ToString(), pubKey, privKey, currentOutputData, Utxos!);

                try
                {
                    // Process the transaction
                    await ProcessTx(tx, txHash);

                    // Update utxos
                    Utxos = Utxos.Except(consumedUtxos).Concat(changeUtxos).ToList();

                    // Update tx hash in the processed.csv file
                    await UpdateProcessFileAsync(currentOutputDataIndices, txHash);

                    // Clear the current output data
                    currentOutputData.Clear();
                    currentOutputDataIndices.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process transaction");
                    break;
                }

            }

            // Process the transaction if there's no more entries to process, but only if it's not empty
            if (tx.Length < _maxTxSize && distributionQueue.Count == 0 && currentOutputData.Count > 0)
            {
                try
                {
                    // Process the transaction
                    await ProcessTx(tx, txHash);

                    // Update utxos
                    Utxos = Utxos.Except(consumedUtxos).Concat(changeUtxos).ToList();

                    // Update tx hash in the processed.csv file
                    await UpdateProcessFileAsync(currentOutputDataIndices, txHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process transaction");
                    break;
                }

                // Clear the current output data
                currentOutputData.Clear();
                currentOutputDataIndices.Clear();
            }
        }

        // Separate processed and unprocessed entries
        var processedEntries = File.ReadAllLines(_processingFile)
            .Skip(1)
            .Where(l => !string.IsNullOrEmpty(l.Split(',').Last()))
            .ToList();

        // Remove the last column (transaction hash) from the unprocessed entries
        var unprocessedEntries = File.ReadAllLines(_processingFile)
            .Skip(1)
            .Where(l => string.IsNullOrEmpty(l.Split(',').Last()))
            .Select(l => string.Join(',', l.Split(',').Take(l.Split(',').Length - 1)))
            .ToList();

        // Get the processing.csv header and attach processedEntries to the body
        var dateTime = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";

        // Write header + processedEntries to a new file
        if (processedEntries.Count > 0)
        {
            var processedFileWithDate = $"{_processedFile}-{dateTime}.csv";
            File.WriteAllLines(processedFileWithDate, [processedHeader!]);
            File.AppendAllLines(processedFileWithDate, processedEntries);
        }

        // Write unprocessedEntries to a new file
        if (unprocessedEntries.Count > 0)
        {
            var unprocessedFileWithDate = $"{_unprocessedFile}-{dateTime}.csv";
            File.WriteAllLines(unprocessedFileWithDate, [unprocessedHeader!]);
            File.AppendAllLines(unprocessedFileWithDate, unprocessedEntries);
        }

        // Delete the processing.csv file
        File.Delete(_processingFile);

        sw.Stop();

        _logger.LogInformation("Finished processing all entries in the distribution file.");
        _logger.LogInformation("Processing took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        Environment.Exit(0);
    }

    private async Task UpdateProcessFileAsync(List<int> indices, string txHash)
    {
        _logger.LogInformation("Successfully processed {x} outputs with txHash: {} ", indices.Count, txHash);
        foreach (int i in indices)
        {
            // Add tx hash to the row in the processed.csv file
            string[] processedLines = File.ReadAllLines(_processingFile);
            string[] processedRow = processedLines[i].Split(',');
            processedRow[^1] = txHash;
            processedLines[i] = string.Join(',', processedRow);
            await File.WriteAllLinesAsync(_processingFile, processedLines);
        }
    }

    private async Task ProcessTx(byte[] tx, string txHash)
    {
        // Process the transaction
        // Submit transaction
        // Execute the POST request
        ByteArrayContent submitPayload = new(tx);
        submitPayload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/cbor");
        HttpResponseMessage submitTxResponse = await SubmitApi.PostAsync("api/submit/tx", submitPayload);

        if (!submitTxResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Error while submitting transaction. Status Code: {submitTxResponse.StatusCode}. Response: {txHash}");
        }

        _logger.LogInformation("Transaction submitted: {txHash}", txHash);
    }


    private async Task UpdateUtxosAsync()
    {
        try
        {
            CardanoNodeClient client = new();
            await client.ConnectAsync(configuration["CardanoNodeSocketPath"]!, configuration.GetValue<uint>("CardanoNetworkMagic"));

            var utxosByAddress = await client.GetUtxosByAddressAsync(Address);
            Utxos = utxosByAddress.Values.Select(uba => Utils.MapUtxoByAddressToUtxo(uba.Key.Value, uba.Value.Value)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update utxos");
        }
    }
}