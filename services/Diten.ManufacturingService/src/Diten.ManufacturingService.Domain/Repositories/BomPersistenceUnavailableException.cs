namespace Diten.ManufacturingService.Domain.Repositories;

/// <summary>
/// Depo işlemi tamamlanamadı (Mongo erişilemez, işlem başarısız). Hiçbir şey yazılmamıştır (tek işlem); çağırana
/// 503 <c>PERSISTENCE_UNAVAILABLE</c> olarak döner — K2 fail-closed: geçmiş yazılamıyorsa BOM da yazılmaz.
/// </summary>
public sealed class BomPersistenceUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
