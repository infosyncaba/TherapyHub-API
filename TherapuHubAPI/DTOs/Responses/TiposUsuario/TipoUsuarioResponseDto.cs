using System.Text.Json.Serialization;

namespace TherapuHubAPI.DTOs.Responses.UserTypes;

public class TipoUsuarioResponseDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("activo")]
    public bool IsActive { get; set; }

    [JsonPropertyName("fechaCreacion")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("autoAssignAllRbts")]
    public bool AutoAssignAllRbts { get; set; }
}
