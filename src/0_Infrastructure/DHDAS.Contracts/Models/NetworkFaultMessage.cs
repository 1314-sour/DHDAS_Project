namespace DHDAS.Contracts.Models;

public record NetworkFaultMessage(
    string NodeName,
    string Endpoint,
    string Error,
    DateTime OccurredAt);
