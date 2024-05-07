using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinecta.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateGetTransactionHistoryByAddressFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET search_path TO coinecta");

            var sql = @"
                DROP FUNCTION IF EXISTS GetTransactionHistoryByAddress(
                    addresses text[],
                    p_offset integer,
                    p_limit integer
                );

                CREATE FUNCTION GetTransactionHistoryByAddress(
                        addresses text[],
                        p_offset integer default 0,
                        p_limit integer default null
                    )
                    RETURNS TABLE (
                        address text,
                        txtype text,
                        lovelace numeric,
                        assets jsonb,
                        slot numeric,
                        txhash text,
                        lockduration text,
                        unlocktime numeric,
                        stakekey text,
                        transferredtoaddress text,
                        outputindex numeric,
                        totalcount bigint
                    ) AS $$
                    BEGIN
                        RETURN QUERY
                        
                        with
                        
                        -- Stake Requests 
                        StakeRequest AS (
                            SELECT
                                srba.""Address"",
                                CASE
                                    WHEN srba.""Status"" = 0 THEN 'StakeRequestPending'
                                    WHEN srba.""Status"" = 1 THEN 'StakeRequestExecuted'
                                    ELSE 'StakeRequestCanceled'
                                END AS txtype,
                                srba.""Amount_Coin"" AS lovelace,
                                srba.""Amount_MultiAssetJson"" AS assets,
                                srba.""Slot"",
                                srba.""TxHash"",
                                srba.""StakePoolJson""->>'LockTime' AS lockduration,
                                0 AS unlocktime,
                                NULL AS stakekey,
                                NULL AS transferredtoaddress,
                                0 AS outputindex
                            FROM (
                                SELECT
                                    *,
                                    ROW_NUMBER() OVER (PARTITION BY ""TxHash"", ""TxIndex"" ORDER BY ""Slot"" DESC) AS rn
                                FROM coinecta.""StakeRequestByAddresses""
                                WHERE ""Address"" = ANY(addresses)
                            ) srba
                            WHERE srba.rn = 1
                        ),
                    
                    -- Stake Position Received
                        LatestPrevAddresses AS (
                            SELECT 
                                n1.*,
                                (
                                    SELECT 
                                        n2.""Address"" 
                                    FROM 
                                        coinecta.""NftsByAddress"" n2
                                    WHERE 
                                        n2.""UtxoStatus"" = 0
                                        AND n2.""PolicyId""  = n1.""PolicyId"" 
                                        AND n2.""AssetName""  = n1.""AssetName"" 
                                        AND n2.""Slot"" < n1.""Slot"" 
                                        AND n2.""TxHash"" != n1.""TxHash"" 
                                    ORDER BY 
                                        n2.""Slot"" DESC
                                    LIMIT 1
                                ) AS prev_address
                            FROM 
                                coinecta.""NftsByAddress"" n1
                            WHERE 
                                n1.""UtxoStatus""  = 0
                                AND n1.""Address""  = ANY(array[addresses])
                        ),
                        StakePositionReceived AS (
                            SELECT  
                                Nft.""Address"",
                                'StakePositionReceived' as txtype,
                                StakeKeys.""Amount_Coin"" AS lovelace,
                                StakeKeys.""Amount_MultiAssetJson"" AS assets,
                                StakeKeys.""Slot"",
                                StakeKeys.""TxHash"",
                                NULL as lockduration,
                                StakeKeys.""LockTime"" AS unlocktime,
                                StakeKeys.""StakeKey"",
                                Nft.""prev_address"" AS transferredtoaddress,
                                StakeKeys.""TxIndex"" AS outputindex
                            FROM 
                                LatestPrevAddresses Nft
                            LEFT JOIN 
                                (SELECT DISTINCT 
                                    ""StakeKey"",
                                    ""LockTime"",
                                    ""Slot"",
                                    ""TxHash"",
                                    ""TxIndex"",
                                    ""Amount_Coin"",
                                    ""Amount_MultiAssetJson""
                                FROM 
                                    coinecta.""StakePositionByStakeKeys""
                                WHERE 
                                    ""UtxoStatus"" = 0) StakeKeys
                            ON 
                                StakeKeys.""StakeKey"" = Nft.""PolicyId"" || substring(Nft.""AssetName"" FROM 8 + 1)
                            WHERE 
                                CASE 
                                    WHEN Nft.prev_address IS NULL OR Nft.prev_address != Nft.""Address"" THEN 1 
                                    ELSE 0 
                                END = 1
                        ),
                        
                        -- Stake Position Transfered
                        LatestNextAddress AS (
                            SELECT 
                                n1.*,
                                (
                                    SELECT 
                                        n2.""Address""
                                    FROM 
                                        coinecta.""NftsByAddress"" n2
                                    WHERE 
                                        n2.""UtxoStatus"" = 0
                                        AND n2.""PolicyId""  = n1.""PolicyId"" 
                                        AND n2.""AssetName""  = n1.""AssetName"" 
                                        AND n2.""Slot"" >= n1.""Slot""  
                                        AND n2.""TxHash"" != n1.""TxHash"" 
                                    ORDER BY 
                                        n2.""Slot"" ASC  
                                    LIMIT 1
                                ) AS next_address,
                                (
                                    SELECT 
                                        n2.""TxHash""
                                    FROM 
                                        coinecta.""NftsByAddress"" n2
                                    WHERE 
                                        n2.""UtxoStatus"" = 0
                                        AND n2.""PolicyId""  = n1.""PolicyId"" 
                                        AND n2.""AssetName""  = n1.""AssetName"" 
                                        AND n2.""Slot"" >= n1.""Slot""  
                                        AND n2.""TxHash"" != n1.""TxHash"" 
                                    ORDER BY 
                                        n2.""Slot"" ASC  
                                    LIMIT 1
                                ) AS next_tx_hash
                            FROM 
                                coinecta.""NftsByAddress"" n1
                            WHERE 
                                n1.""UtxoStatus"" = 1
                                AND n1.""Address""  = ANY(addresses)
                        ),
                        StakePositionTransfered as (
                            SELECT  
                                    Nft.""Address"",
                                    'StakePositionTransferred' as txtype,
                                    StakeKeys.""Amount_Coin"" AS lovelace,
                                    StakeKeys.""Amount_MultiAssetJson"" AS assets,
                                    Nft.""Slot"",
                                    Nft.next_tx_hash AS txhash,
                                    null as lockduration,
                                    StakeKeys.""LockTime"" AS unlocktime,
                                    Nft.""PolicyId"" || substring(Nft.""AssetName"" FROM 8 + 1) AS stakekey,
                                    Nft.next_address AS transferredtoaddress,
                                    Nft.""OutputIndex"" AS outputindex
                                FROM 
                                    LatestNextAddress Nft
                                LEFT JOIN 
                                    coinecta.""StakePositionByStakeKeys"" StakeKeys ON StakeKeys.""StakeKey"" = Nft.""PolicyId"" || substring(Nft.""AssetName"" FROM 8 + 1)
                                WHERE 
                                    CASE 
                                        WHEN Nft.next_address != Nft.""Address"" AND Nft.next_address IS NOT NULL THEN 1 
                                        ELSE 0 
                                    END = 1
                        ),
                        
                        
                        -- Stake Position Redeemed
                        LatestSpentStakePositions AS (
                        SELECT
                            spbsk.""StakeKey"",
                            spbsk.""Slot"",
                            spbsk.""TxHash"",
                            spbsk.""Amount_Coin"",
                            spbsk.""Amount_MultiAssetJson"",
                            spbsk.""LockTime"",
                            spbsk.""TxIndex""
                        FROM
                            coinecta.""StakePositionByStakeKeys"" spbsk
                        WHERE
                            spbsk.""UtxoStatus"" = 1
                        ),
                        LastOwnerAddresses AS (
                            SELECT
                                nft.""Address"",
                                nft.""PolicyId"",
                                nft.""AssetName"",
                                ROW_NUMBER() OVER (PARTITION BY nft.""PolicyId"", substring(nft.""AssetName"" FROM 8 + 1) ORDER BY nft.""Slot"" DESC) AS rn
                            FROM
                                coinecta.""NftsByAddress"" nft
                            where
                                nft.""UtxoStatus"" = 1
                        ),
                        StakePositionsRedeemed as (
                            SELECT
                                nft.""Address"",
                                'StakePositionRedeemed' as txtype,
                                sp.""Amount_Coin"" AS lovelace,
                                sp.""Amount_MultiAssetJson"" AS assets,
                                sp.""Slot"",
                                sp.""TxHash"" as txhash,
                                null as lockduration,
                                sp.""LockTime"" AS unlocktime,
                                sp.""StakeKey"" as stakekey,
                                null AS transferredtoaddress,
                                sp.""TxIndex"" AS outputindex
                            FROM
                                LatestSpentStakePositions sp
                            JOIN
                                LastOwnerAddresses nft ON sp.""StakeKey"" = nft.""PolicyId"" || substring(nft.""AssetName"" FROM 8 + 1)
                            WHERE
                                nft.rn = 1 and nft.""Address"" = ANY(addresses)
                        )
                        
                        -- Comnbine Transaction Histories and Paginate
                        select *,
                        COUNT(*) OVER() as total_count
                        from(
                            select * from StakeRequest
                            union all
                            select * from StakePositionReceived
                            union all 
                            select * from StakePositionTransfered
                            union all 
                            select * from StakePositionsRedeemed
                        ) as combined
                        order by combined.""Slot"" desc
                        offset p_offset
                        limit p_limit;

                    END;
                    $$ LANGUAGE plpgsql;
            ";

            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET search_path TO coinecta");

            var sql = @"
                DROP FUNCTION IF EXISTS GetTransactionHistoryByAddress(
                    addresses text[],
                    p_offset integer,
                    p_limit integer
                );
            ";

            migrationBuilder.Sql(sql);
        }
    }
}
