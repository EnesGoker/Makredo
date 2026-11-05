namespace Makredo.WinForms.Controls;

internal readonly record struct EventListFilterCriteria(
    string? SearchTerm,
    EventListTypeFilterKind TypeFilter,
    EventListSmartFilterKind SmartFilter);
