using System.Text.Json;
using Coinecta.Data;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;

namespace Coinecta.API.Modules.V1;

public class UtxoHandler(IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration)
{
    public async Task<IResult> UpdateUtxoTrackerAsync(List<string> addresses)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        IEnumerable<UtxoByAddress>? utxoByAddresses = await dbContext.UtxosByAddress.Where(x => addresses.Contains(x.Address)).ToListAsync();

        int expirationMilliseconds = int.Parse(configuration["UtxosByAddressExpirationMillisecond"] ?? "300000");
        addresses.ToList().ForEach(address =>
        {
            UtxoByAddress? utxoByAddress = utxoByAddresses.FirstOrDefault(x => x.Address == address);
            DateTimeOffset expirationTime = DateTimeOffset.UtcNow.AddMilliseconds(expirationMilliseconds);

            if (utxoByAddress is null)
            {
                UtxoByAddress newUtxosByAddress = new()
                {
                    Address = address,
                    LastUpdated = DateTimeOffset.MinValue,
                    LastRequested = expirationTime,
                    UtxoListCbor = []
                };

                dbContext.UtxosByAddress.Add(newUtxosByAddress);
            }
            else
            {
                utxoByAddress.LastRequested = expirationTime;
            }
        });

        await dbContext.SaveChangesAsync();

        return Results.Ok(utxoByAddresses);
    }

    public IResult FetchConfig()
    {
        return Results.Ok(configuration.AsEnumerable());
    }
}