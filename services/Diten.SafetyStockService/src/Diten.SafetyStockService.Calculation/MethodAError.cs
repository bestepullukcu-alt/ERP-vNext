namespace Diten.SafetyStockService.Calculation;

public enum MethodAError
{
    MissingRequest,
    MissingFixture,
    MissingFixtureVersion,
    InvalidScope,
    MissingBaseUom,
    InvalidDateRange,
    MissingDays,
    InvalidDay,
    DayOutsideRange,
    DuplicateDay,
    ScopeMismatch,
    BaseUomMismatch,
    UnknownDemand,
    NegativeDemand,
    MissingDay,
    MissingCoverageDays,
    InvalidCoverageDays,
    ArithmeticOverflow
}
