namespace Luggage;
public class DiagnosticRepository(Store db) : BaseRepository<Diagnostic>(db), IDiagnosticRepository;
