namespace TherapuHubAPI.DTOs.Responses.ActorRelationships;

public class RbtAssignmentResponseDto
{
    public int StaffId { get; set; }
    public int ActorId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsAssigned { get; set; }
    public long? RelationshipId { get; set; }
    public bool IsAutoAssigned { get; set; }
}
