namespace ApiGateway.Models;

public class Deployment
{
    public int DeploymentId { get; set; }

    public DateTime? DeploymentDate { get; set; }

    public int Status { get; set; }
}
