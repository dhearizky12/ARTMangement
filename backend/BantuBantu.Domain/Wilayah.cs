namespace BantuBantu.Domain;
public enum VillageType { Kelurahan, Desa, DesaAdat }
public class Province { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
public class Regency { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string ProvinceId { get; set; } = ""; public Province Province { get; set; } = null!; }
public class District { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string RegencyId { get; set; } = ""; public Regency Regency { get; set; } = null!; }
public class Village { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string DistrictId { get; set; } = ""; public District District { get; set; } = null!; public VillageType Type { get; set; } }
