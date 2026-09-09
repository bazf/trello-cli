using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrelloCli.Models;

/// <summary>A custom field defined on a board.</summary>
public class CustomField
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("idModel")]
    public string BoardId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>text, number, date, checkbox or list.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("pos")]
    public double Pos { get; set; }

    /// <summary>Present for list fields; these are the ids --set-custom-field --option accepts.</summary>
    [JsonPropertyName("options")]
    public List<CustomFieldOption> Options { get; set; } = new();
}

public class CustomFieldOption
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("pos")]
    public double Pos { get; set; }
}

/// <summary>A custom field's value on one card.</summary>
public class CustomFieldItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("idCustomField")]
    public string CustomFieldId { get; set; } = string.Empty;

    [JsonPropertyName("idModel")]
    public string CardId { get; set; } = string.Empty;

    /// <summary>Shape depends on the field type, so it is passed through as Trello sends it.</summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    /// <summary>The chosen option, for list fields.</summary>
    [JsonPropertyName("idValue")]
    public string? SelectedOptionId { get; set; }
}
