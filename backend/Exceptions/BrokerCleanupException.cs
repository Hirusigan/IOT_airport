namespace Luggage;

public sealed class BrokerCleanupException(Exception innerException) : Exception(
    "The database was cleared, but airport broker cleanup did not finish.",
    innerException);
