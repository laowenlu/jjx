using Contracts.Domain.Enums;

namespace Contracts.Domain.Logic
{
    /// <summary>
    /// SampleLogicContract is a basic logic contract holding example data and a SampleEnum value.
    /// </summary>
    public class SampleLogicContract
    {
        /// <summary>
        /// The name or label for this contract.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A value demonstrating use of a cross-contract enum.
        /// </summary>
        public SampleEnum Status { get; set; }
    }
}