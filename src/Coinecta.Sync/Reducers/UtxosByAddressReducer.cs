using Cardano.Sync;
using Cardano.Sync.Reducers;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using Coinecta.Data;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using PeterO.Cbor2;

namespace Coinecta.Sync.Reducers;

public class UtxosByAddressReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<UtxosByAddressReducer> logger
) : IReducer
{
    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<UtxosByAddressReducer> _logger = logger;
    public async Task RollBackwardAsync(NextResponse response)
    {
        _logger.LogInformation("Rolling back UtxosByAddressReducer");
        await Task.CompletedTask;
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _logger.LogInformation("Rolling forward UtxosByAddressReducer");
        _dbContext = dbContextFactory.CreateDbContext();

        _logger.LogInformation("Updating UtxosByAddress");
        int expirationMilliseconds = configuration.GetValue<int>("UtxosByAddressExpirationMillisecond");
        DateTimeOffset thresholdTime = DateTimeOffset.UtcNow.AddMilliseconds(-expirationMilliseconds);
        _logger.LogInformation("Threshold time: {ThresholdTime}", thresholdTime);

        _logger.LogInformation("Fetching UtxosByAddress");
        List<UtxoByAddress> trackedUtxosByAddress = await _dbContext.UtxosByAddress
            .Where(x => x.LastRequested >= thresholdTime)
            .ToListAsync();

        _logger.LogInformation("Updating {Count} UtxosByAddress", trackedUtxosByAddress.Count);
        // Process by batches
        int batchSize = 10;
        int batchCount = (int)Math.Ceiling((double)trackedUtxosByAddress.Count / batchSize);
        int currentBatch = 0;

        _logger.LogInformation("Batch count: {BatchCount}", batchCount);
        while (currentBatch < batchCount)
        {
            _logger.LogInformation("Processing batch {CurrentBatch}", currentBatch);
            IEnumerable<UtxoByAddress> batch = trackedUtxosByAddress.Skip(currentBatch * batchSize).Take(batchSize);
            await Task.WhenAll(batch.Select(async utxoByAddress =>
            {
                try
                {
                    _logger.LogInformation("Updating UtxosByAddress for {Address}", utxoByAddress.Address);
                    await UpdateUtxosByAddressAsync(utxoByAddress.Address, utxoByAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UtxosByAddress for {Address}", utxoByAddress.Address);
                }
            }));

            currentBatch++;
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private async Task UpdateUtxosByAddressAsync(string address, UtxoByAddress utxoByAddress)
    {
        _logger.LogInformation("Updating UtxosByAddress for {Address}", address);
        if (string.IsNullOrEmpty(address)) return;

        _logger.LogInformation("Fetching UtxosByAddress for {Address}", address);
        CardanoNodeClient client = new();
        await client.ConnectAsync(configuration["CardanoNodeSocketPath"]!, configuration.GetValue<uint>("CardanoNetworkMagic"));

        _logger.LogInformation("Fetching UtxosByAddress for {Address}", address);
        Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxos = await client.GetUtxosByAddressAsync(address);

        // Update state
        _logger.LogInformation("Updating UtxosByAddress for {Address}", address);
        utxoByAddress.UtxoListCbor = utxos.Values.Select(u =>
            Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();
        utxoByAddress.LastUpdated = DateTime.UtcNow;
    }
}