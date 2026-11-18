namespace Luggage;

public class DiagnosticRepository(TrackingDbContext db) : BaseRepository<Diagnostic>(db), IDiagnosticRepository;
