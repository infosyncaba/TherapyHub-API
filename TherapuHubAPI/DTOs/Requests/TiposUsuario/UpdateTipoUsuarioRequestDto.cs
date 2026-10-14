using System.Text.Json.Serialization;

namespace TherapuHubAPI.DTOs.Requests.UserTypes;

public class UpdateTipoUsuarioRequestDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("activo")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("autoAssignAllRbts")]
    public bool AutoAssignAllRbts { get; set; }
}
