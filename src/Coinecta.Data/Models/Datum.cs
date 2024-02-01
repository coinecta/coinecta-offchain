namespace Coinecta.Data.Models;

public enum DatumType {
    NoDatum,
    DatumHash,
    InlineDatum
}

public record Datum(DatumType Type, byte[] Data);