using CardanoSharp.Wallet.Extensions.Models;
using CborSerialization;
using Coinecta;
using Coinecta.Data;
using Coinecta.Data.Models.Datums;
using Coinecta.Data.Models.Reducers;
using Coinecta.Sync.Reducers;
using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;

public class StakeRequestByAddressReducer(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<StakeRequestByAddressReducer> logger
) : IReducer
{

    private CoinectaDbContext _dbContext = default!;
    private readonly ILogger<StakeRequestByAddressReducer> _logger = logger;

    public async Task RollBackwardAsync(NextResponse response)
    {

        _dbContext = dbContextFactory.CreateDbContext();
        var rollbackSlot = response.Block.Slot;
        var schema = configuration.GetConnectionString("CoinectaContextSchema");
        await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM \"{schema}\".\"StakeRequestByAddresses\" WHERE \"Slot\" > {rollbackSlot}");
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();
        await ProcessInputAync(response);
        await ProcessOutputAync(response);
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private async Task ProcessInputAync(NextResponse response)
    {
        // Collect input id and index concatenated by # seperator
        var inputIds = response.Block.TransactionBodies.SelectMany(txBody => txBody.Inputs.Select(input => input.Id.ToHex()+"#" +input.Index)).ToList();

        // Find all stake requests by address that have input id and index
        var stakeRequestsByAddress = await _dbContext.StakeRequestByAddresses.Where(s => inputIds.Contains(
            s.TxHash + "#" + s.TxIndex
        )).ToListAsync();

        // Update status of stake requests to cancelled
        foreach (var stakeRequestByAddress in stakeRequestsByAddress)
        {
            stakeRequestByAddress.Status = StakeRequestStatus.Cancelled;
        }
    }

    private Task ProcessOutputAync(NextResponse response)
    {
        foreach (var txBody in response.Block.TransactionBodies)
        {
            foreach (var output in txBody.Outputs)
            {
                var addressBech32 = output.Address.ToBech32();
                if (addressBech32.StartsWith("addr"))
                {
                    var address = new Address(output.Address.ToBech32());
                    var pkh = Convert.ToHexString(address.GetPublicKeyHash()).ToLowerInvariant();
                    if (pkh == configuration["CoinectaStakeProxyValidatorHash"])
                    {
                        if (output.Datum is not null && output.Datum.Type == DatumType.InlineDatum)
                        {
                            var datum = output.Datum.Data;
                            try
                            {
                                var stakePoolDatum = CborConverter.Deserialize<StakePoolProxy<NoDatum>>(datum);
                                var entityUtxo = Utils.MapTransactionOutputEntity(txBody.Id.ToHex(), response.Block.Slot, output);
                                var stakeRequestByAddress = new StakeRequestByAddress
                                {
                                    Address = addressBech32,
                                    Slot = response.Block.Slot,
                                    TxHash = txBody.Id.ToHex(),
                                    TxIndex = output.Index,
                                    Amount = entityUtxo.Amount,
                                    Status = StakeRequestStatus.Pending,
                                    StakePoolProxy = stakePoolDatum
                                };

                                _dbContext.StakeRequestByAddresses.Add(stakeRequestByAddress);
                            }
                            catch
                            {
                                _logger.LogError("Error deserializing stake pool proxy datum: {datum} for {txHash}#{txIndex}", 
                                    Convert.ToHexString(datum).ToLowerInvariant(), 
                                    txBody.Id.ToHex(), 
                                    output.Index
                                );
                            }
                        }
                    }
                }
            }
        }
        return Task.CompletedTask;
    }
}