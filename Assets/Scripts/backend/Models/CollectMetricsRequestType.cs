#nullable enable
#pragma warning disable CS8618
using EdjCase.ICP.Candid.Mapping;

namespace Cosmicrafts.backend.Models
{
	public enum CollectMetricsRequestType
	{
		[CandidName("force")]
		Force,
		[CandidName("normal")]
		Normal
	}
}