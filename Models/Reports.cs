namespace TestingSystem.Models;

/// <summary>Плаский рядок для експорту результатів (CSV/Excel/PDF).</summary>
public record ResultExportRow(
    string Student, string Group, string Test, double Score, double MaxScore,
    double Percent, bool Passed, ResultStatus Status, DateTime Date, int Violations);

/// <summary>Складність питання: частка спроб, де відповідь не отримала повний бал.</summary>
public record QuestionDifficulty(string TestTitle, string QuestionText, int Attempts, double WrongRatePercent, bool IsHard);

public record CertificateData(string StudentName, string TestTitle, double Percent, DateTime Date, string Number);

public record ReportData(string Title, string FilterDescription, IReadOnlyList<ResultExportRow> Rows, IReadOnlyList<QuestionDifficulty> HardQuestions);
