namespace TestingSystem.Models;

public enum Role { Admin, Teacher, Student }

public enum QuestionType { SingleChoice, MultipleChoice, TextAnswer, Matching, Sequence, FileUpload }

public enum TextEvaluationType { AutoExactMatch, AutoContains, ManualTeacherReview }

/// <summary>Статус сесії/результату (кінцевий автомат описаний у TestSessionController).</summary>
public enum ResultStatus { InProgress, PendingTeacherReview, Completed, Expired }
