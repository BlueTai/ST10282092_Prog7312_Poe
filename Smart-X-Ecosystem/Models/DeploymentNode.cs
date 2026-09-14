using System.Collections.Generic;

namespace Smart_X_Ecosystem.Models
{
    public class DeploymentNode
    {
        public string NodeId { get; set; }
        public bool IsConfiguredSafely { get; set; }
        public List<DeploymentNode> SubNodes { get; set; } = new List<DeploymentNode>();

        public bool ValidateConfigurationTree()
        {
            if (!IsConfiguredSafely) return false; // Base case

            foreach (var node in SubNodes)
            {
                if (!node.ValidateConfigurationTree()) return false;
            }
            return true;
        }
    }
}