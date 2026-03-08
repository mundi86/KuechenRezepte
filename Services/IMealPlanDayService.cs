namespace KuechenRezepte.Services;

public interface IMealPlanDayService
{
    Task<MealPlanDayResult> GetDayAsync(DateOnly datum, CancellationToken cancellationToken = default);
}
