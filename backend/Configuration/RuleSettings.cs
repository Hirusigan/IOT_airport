namespace Luggage;

public record RuleSettings(int LoadingCutoffSeconds, int GraceSeconds, int StaleSeconds);
