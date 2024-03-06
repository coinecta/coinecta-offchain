using System.Text.Json;
using Cardano.Sync.Data.Models;
using Cardano.Sync.Data.Models.Datums;
using Cardano.Sync.Reducers;
using Coinecta.Data;
using Coinecta.Data.Models.Enums;
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
        ulong rollbackSlot = response.Block.Slot;
        _dbContext.UtxosByAddress.RemoveRange(_dbContext.UtxosByAddress.Where(s => s.Slot > rollbackSlot));
        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }

    public async Task RollForwardAsync(NextResponse response)
    {
        _dbContext = dbContextFactory.CreateDbContext();

        List<string>? utxoAddresses = JsonSerializer.Deserialize<List<string>>(configuration.GetValue<string>("UtxoAddresses")!);
        foreach (TransactionBody? txBody in response.Block.TransactionBodies.ToList())
        {
            foreach (TransactionInput input in txBody.Inputs)
            {
                // First check in-memory data
                UtxoByAddress? utxoByAddress = _dbContext.UtxosByAddress.Local
                    .Where(s => s.TxHash == input.Id.ToHex())
                    .Where(s => s.TxIndex == input.Index)
                    .FirstOrDefault();

                // Then check the database
                utxoByAddress ??= await _dbContext.UtxosByAddress
                    .AsNoTracking()
                    .Where(s => s.TxHash == input.Id.ToHex())
                    .Where(s => s.TxIndex == input.Index)
                    .FirstOrDefaultAsync();

                if (utxoByAddress is not null)
                {
                    _dbContext.UtxosByAddress.Add(new()
                    {
                        Address = utxoByAddress.Address,
                        TxHash = utxoByAddress.TxHash,
                        TxIndex = utxoByAddress.TxIndex,
                        Slot = response.Block.Slot,
                        Status = UtxoStatus.Spent
                    });
                }
            }

            foreach (PallasDotnet.Models.TransactionOutput output in txBody.Outputs)
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
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.Dispose();
    }
}