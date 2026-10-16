# MOD-0188 forecast methods: isolated fixture evidence

Date: 2026-10-07. Scope: WP-MOD0188-FORECAST-METHODS. This document does not
certify accepted history, a ForecastRun, AC-D09/D11, or live integration.

## Package decision

No package was added. The inspected current .NET choices did not provide a
verified, maintained implementation of all seven approved candidates:

| Primary package source | License and inspected version | Verified scope | Decision |
| --- | --- | --- | --- |
| [Microsoft.ML.TimeSeries](https://www.nuget.org/packages/Microsoft.ML.TimeSeries/) and [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.transforms.timeseries.ssaforecastingestimator) | MIT; 5.0.0 | SSA forecasting is documented; it is not one of the seven named methods. | Do not relabel SSA. |
| [Math.NET Numerics](https://github.com/mathnet/mathnet-numerics) and its [license](https://github.com/mathnet/mathnet-numerics/blob/master/LICENSE.md) | MIT; inspected project documentation | Numerical primitives are documented; coverage of all seven forecast methods was not verified. | Do not claim method support. |
| [NW.UnivariateForecasting](https://www.nuget.org/packages/NW.UnivariateForecasting) and [source](https://github.com/numbworks/NW.UnivariateForecasting) | MIT; 4.2.1 | Univariate sliding-window forecasting is documented; the seven exact methods are not. | No method substitution. |
| [Skender.Stock.Indicators](https://www.nuget.org/packages/Skender.Stock.Indicators) and [project documentation](https://dotnet.stockindicators.dev/) | Apache-2.0; 2.7.3 | Financial SMA/EMA indicators, not a verified seven-method demand forecast engine; v2 maintenance ends in 2026. | No dependency added. |

The **Naive** and **Seasonal Naive** benchmark formulas were implemented locally,
openly and narrowly in `FixtureForecastEngine`. Their definitions are from the
authors' [Forecasting: Principles and Practice, 3rd ed., simple methods](https://otexts.com/fpp3/simple-methods.html).
The annual seasonal lag is explicitly 52 weeks. They are not presented as
third-party library algorithms. **Moving Average, Simple Exponential Smoothing,
Holt trend, Holt-Winters and Bias-corrected Croston remain unimplemented**;
requesting one throws instead of silently substituting a method. Exact smoothing
variants, parameter defaults and a suitable maintained .NET implementation
remain technical design work. The 26/104-week and intermittent gates still run
from the existing versioned fixture eligibility policy.

## Product quality check

[SAP IBP's algorithm comparison](https://help.sap.com/docs/SAP_INTEGRATED_BUSINESS_PLANNING/feae3cea3cc549aaa9d9de7d363a83e6/3e60a6d96fe446229590f0d2eeaf9cf9.html)
distinguishes trend, seasonality and intermittency. Its
[multiple forecast guidance](https://help.sap.com/docs/SAP_INTEGRATED_BUSINESS_PLANNING/feae3cea3cc549aaa9d9de7d363a83e6/65b1da1c2630409885b96c4eec9b61d0.html)
shows why individual method output and an explicit comparison measure matter.
[Oracle demand planning documentation](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25c/faupc/forecasting-methods-for-demand-plans.html)
describes distinct treatment for sparse demand. These are quality references,
not imported product behavior: MOD-0188 retains the approved human selection,
no automatic ranking, and its own Missing/Unknown/stockout semantics.

## Fixture behavior and limits

The fixture engine checks the as-of boundary, reruns eligibility for each
candidate, gives only the training prefix to the existing rolling-origin
evaluator, generates exactly 52 future weeks and carries policy version plus
method parameters. Side-by-side WAPE/MAE/bias/MASE and horizon metrics come
from the existing comparison component. No winner, fallback, confidence score,
API, Draft persistence or publication is produced. A complete seasonal
backtest needs 104 training plus a 52-week holdout; shorter eligible history
can still produce a fixture forecast while evaluation reports insufficient
history.

Control Tower K13 and Ali's manual acceptance remain separate. Live accepted
history, policy authority and the remaining five methods are open gates.
