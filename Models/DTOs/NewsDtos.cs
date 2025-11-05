using System;

namespace Cuzdan360Backend.Models.DTOs
{
    /// <summary>
    /// Frontend'e göndereceğimiz temizlenmiş haber DTO'su
    /// Not: Next.js 'lib/types.ts' dosyanızdaki 'NewsArticle' ile eşleşir
    /// </summary>
    /// <param name="Id">Haberin ID'si (RSS'ten veya GUID)</param>
    /// <param name="Headline">Haber Başlığı</param>
    /// <param name="Source">Haber Kaynağı (Örn: "Dünya Gazetesi")</param>
    /// <param name="Time">Yayınlanma Zamanı (Formatlanmış string)</param>
    /// <param name="ImageUrl">Resim URL'si (varsa)</param>
    /// <param name="Url">Haberin detay linki</param>
    public record NewsArticleDto(
        string Id,
        string Headline,
        string Source,
        string Time,
        string? ImageUrl, 
        string Url
    );
}