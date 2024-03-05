using System.Text.Json;
using Cardano.Sync.Data.Models;
using Cardano.Sync.Data.Models.Datums;
using Cardano.Sync.Reducers;
using Coinecta.Data;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;
using PallasDotnet.Models;

namespace Coinecta.Sync.Reducers;

[ReducerDepends(typeof(TransactionOutputReducer<>))]
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
        _dbContext = dbContextFactory.CreateDbContext();
        var rollbackSlot = response.Block.Slot;
        _dbContext.UtxosByAddress.RemoveRange(_dbContext.UtxosByAddress.Where(s => s.Slot > rollbackSlot));
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();

        var utxoAddresses = JsonSerializer.Deserialize<List<string>>(configuration.GetValue<string>("UtxoAddresses")!);
        foreach (var txBody in response.Block.TransactionBodies.ToList())
        {
            foreach (var input in txBody.Inputs)
            {
                var resolvedInput = await _dbContext.TransactionOutputs
                    .Where(o => o.Id == input.Id.ToHex())
                    .Where(o => o.Index == input.Index)
                    .FirstOrDefaultAsync();

                if (resolvedInput is not null && utxoAddresses!.Contains(resolvedInput.Address))
                {
                    _dbContext.UtxosByAddress.Add(new()
                    {
                        Address = resolvedInput.Address,
                        TxHash = input.Id.ToHex(),
                        TxIndex = input.Index,
                        Slot = response.Block.Slot,
                        Status = UtxoStatus.Spent
                    });
                }
            }

            foreach (var output in txBody.Outputs)
            {
                if (utxoAddresses!.Contains(output.Address.ToBech32()))
                {
                    _dbContext.UtxosByAddress.Add(new()
                    {
                        Address = output.Address.ToBech32(),
                        TxHash = Convert.ToHexString(txBody.Id.Bytes).ToLowerInvariant(),
                        TxIndex = output.Index,
                        Slot = response.Block.Slot,
                        TxOutCbor = output.Raw,
                        Status = UtxoStatus.Unspent
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
        }
        _dbContext.Dispose();
    }
}