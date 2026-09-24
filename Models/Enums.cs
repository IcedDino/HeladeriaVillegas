namespace HeladeriaPOS.Models;

public enum ProductCategory
{
    Snacks,
    Helados,
    Especialidades
}

public enum ProductType
{
    PapasSabritas,
    Fritura,
    SopaPalomitas,
    Barquillo,
    Vaso,
    Canasta,
    Envase,
    Malteada,
    Copa,
    BananaSplit,
    TresMarias,
    Custom
}

public enum TicketStatus
{
    Open,
    Paid,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,
    Mixed
}

public enum ModifierType
{
    Preparation,
    ExtraIngredient,
    ExtraScoop,
    Chantilly,
    Other
}

public enum SnackPreparation
{
    Normal,
    PreparedAll,
    MissingIngredient
}

public enum IceCreamSize
{
    Chico,
    Mediano,
    Grande,
    Jumbo,
    Doble,
    Triple,
    MedioLitro,
    UnLitro,
    CincoLitros,
    DoceLitros
}

public enum IceCreamPreparation
{
    None,
    SingleIngredient,
    ChocolateOrJamAndCereal
}
