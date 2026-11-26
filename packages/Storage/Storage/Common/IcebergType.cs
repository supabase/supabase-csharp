namespace Supabase.Storage.Common;

/// <summary>
/// The type of an Iceberg field: an <see cref="IcebergPrimitiveType"/>, or a nested <see cref="IcebergStructType"/>,
/// <see cref="IcebergListType"/> or <see cref="IcebergMapType"/>.
/// </summary>
public abstract record IcebergType
{
    private protected IcebergType()
    {
    }
}
