namespace Diten.Web.Models;

/// <summary>Friendly status-page model for 401/403/404 (and a generic fallback). UX only.</summary>
public sealed class StatusPageViewModel
{
    public int Code { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? RequestId { get; init; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public static StatusPageViewModel For(int code, string? requestId) => code switch
    {
        401 => new StatusPageViewModel { Code = 401, Title = "Oturum gerekli", Message = "Bu sayfayı görüntülemek için giriş yapmanız gerekiyor.", RequestId = requestId },
        403 => new StatusPageViewModel { Code = 403, Title = "Erişim reddedildi", Message = "Bu kaynağa erişim yetkiniz bulunmuyor.", RequestId = requestId },
        404 => new StatusPageViewModel { Code = 404, Title = "Sayfa bulunamadı", Message = "Aradığınız sayfa bulunamadı veya taşınmış olabilir.", RequestId = requestId },
        _ => new StatusPageViewModel { Code = code, Title = "Bir sorun oluştu", Message = "İsteğiniz işlenirken beklenmeyen bir durum oluştu.", RequestId = requestId }
    };
}
