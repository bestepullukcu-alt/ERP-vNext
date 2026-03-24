using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public class Country : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Iso2Code { get; set; } = string.Empty;
    public string Iso3Code { get; set; } = string.Empty;
    public string? PhoneCode { get; set; }
    public bool IsActive { get; set; } = true;
}
