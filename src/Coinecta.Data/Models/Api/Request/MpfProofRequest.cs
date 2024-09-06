namespace Coinecta.Data.Models.Api.Request;

public record MpfProofRequest(Dictionary<string, string> Data, string Key);