namespace Luggage;
public class PersistenceConflictException(string message, Exception inner) : Exception(message, inner);
