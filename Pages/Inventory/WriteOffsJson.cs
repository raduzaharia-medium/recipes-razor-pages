using System.Text.Json.Serialization;

namespace Recipes.Pages.Inventory;

/// <summary>Source-generated serializer for the write-off list handed from the stock-take to the print page.</summary>
[JsonSerializable(typeof(List<WrittenOffProduct>))]
internal partial class WriteOffsJson : JsonSerializerContext;
