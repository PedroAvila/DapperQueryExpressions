using Dapper.Contrib.Extensions;

namespace PAN.DapperLambdaToSql.Tests;

/// <summary>
/// Espeja la entidad Role de un dominio real: tiene una columna llamada <c>Key</c>,
/// palabra reservada tanto en MySQL como en SQL Server.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public int? Code { get; set; }
    public int? GymId { get; set; }
    public string? Name { get; set; }
    public string? Key { get; set; }
    public bool IsSystemRole { get; set; }
}

/// <summary>
/// Espeja una entidad User: llaves foráneas con el mismo patrón de nombre que la
/// convención <c>Clase+Id</c>, y propiedades de navegación marcadas <c>[Write(false)]</c>.
/// </summary>
public class User
{
    public int Id { get; set; }
    public int? GymId { get; set; }
    public string? Name { get; set; }
    public int RoleId { get; set; }

    [Write(false)]
    public Role? Role { get; set; }

    [Write(false)]
    public Gym? Gym { get; set; }
}

public class Gym
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>Clave primaria en mayúsculas: solo la resuelve la comparación insensible.</summary>
public class Invoice
{
    public int ID { get; set; }
    public string? Concept { get; set; }
}

/// <summary>Clave primaria por convención <c>Clase+Id</c>.</summary>
public class Warehouse
{
    public int WarehouseId { get; set; }
    public string? Name { get; set; }
}

/// <summary>Tiene <c>Id</c> y además un <c>[ExplicitKey]</c>: debe ganar el atributo.</summary>
public class Product
{
    public int Id { get; set; }

    [ExplicitKey]
    public string? Sku { get; set; }

    public string? Name { get; set; }
}

/// <summary>Clave por <c>[Key]</c> con un nombre que ninguna convención encontraría.</summary>
public class Ticket
{
    [Key]
    public int Folio { get; set; }

    public string? Subject { get; set; }
}

/// <summary>
/// Sin clave resoluble. Tiene llaves foráneas que terminan en <c>Id</c> para comprobar
/// que la convención no las toma como primarias.
/// </summary>
public class Detail
{
    public int GymId { get; set; }
    public int CustomerId { get; set; }
    public string? Note { get; set; }
}

/// <summary>Columna calculada por el motor.</summary>
public class Employee
{
    public int Id { get; set; }
    public string? FirstName { get; set; }

    [Computed]
    public string? FullName { get; set; }
}

[Table("Gimnasios")]
public class BrandedGym
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

[Table("dbo.Sucursales")]
public class QualifiedBranch
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
