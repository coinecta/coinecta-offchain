using Coinecta.Data;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.API.Modules.V1;

public class UtxoHandler(IDbContextFactory<CoinectaDbContext> dbContextFactory)
{
    public async Task<IResult> UpdateUtxoTrackerAsync(List<string> addresses)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        IEnumerable<UtxoByAddress>? utxoByAddresses = await dbContext.UtxosByAddress.Where(x => addresses.Contains(x.Address)).ToListAsync();

        addresses.ToList().ForEach(address =>
        {
            UtxoByAddress? utxoByAddress = utxoByAddresses.FirstOrDefault(x => x.Address == address);
            long currentTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (utxoByAddress is null)
            {
                UtxoByAddress newUtxosByAddress = new()
                {
                    Address = address,
                    LastUpdated = DateTimeOffset.MinValue.ToUnixTimeMilliseconds(),
                    LastRequested = currentTimeStamp,
                    UtxoListCbor = []
                };

                dbContext.UtxosByAddress.Add(newUtxosByAddress);
            }
            else
            {
                utxoByAddress.LastRequested = currentTimeStamp;
            }
        });

        await dbContext.SaveChangesAsync();

        return Results.Ok(utxoByAddresses);
    }
}