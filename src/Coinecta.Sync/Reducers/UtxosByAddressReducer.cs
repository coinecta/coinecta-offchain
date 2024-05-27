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
        await Task.CompletedTask;
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();

        int expirationMinutes = configuration.GetValue<int>("UtxosByAddressExpirationMinutes");
        var thresholdTime = DateTime.UtcNow.AddMinutes(-expirationMinutes);

        List<UtxoByAddress> trackedUtxosByAddress = await _dbContext.UtxosByAddress
            .Where(x => x.LastRequested >= thresholdTime)
            .ToListAsync();


        // @TODO: Process by batches
        await Task.WhenAll(trackedUtxosByAddress.Select(async utxoByAddress =>
        {
            try
            {
                await UpdateUtxosByAddressAsync(utxoByAddress.Address, utxoByAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating UtxosByAddress for {Address}", utxoByAddress.Address);
            }
        }));

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    private async Task UpdateUtxosByAddressAsync(string address, UtxoByAddress utxoByAddress)
    {
        CardanoNodeClient client = new();
        await client.ConnectAsync(configuration["CardanoNodeSocketPath"]!, configuration.GetValue<uint>("CardanoNetworkMagic"));

        Cardano.Sync.Data.Models.Experimental.UtxosByAddress utxos = await client.GetUtxosByAddressAsync(address);

        // Update state
        utxoByAddress.UtxoListCbor = utxos.Values.Select(u =>
            Convert.ToHexString(CBORObject.NewArray().Add(u.Key.Value.GetCBOR()).Add(u.Value.Value.GetCBOR()).EncodeToBytes()).ToLowerInvariant()).ToList();
        utxoByAddress.LastUpdated = DateTime.UtcNow;
    }
}