#nullable enable
#pragma warning disable CS8618
using EdjCase.ICP.Candid.Mapping;
using Cosmicrafts.backend.Models;

namespace Cosmicrafts.backend.Models
{
	public class MetricsRequest
	{
		[CandidName("parameters")]
		public GetMetricsParameters Parameters { get; set; }

		public MetricsRequest(GetMetricsParameters parameters)
		{
			this.Parameters = parameters;
		}

		public MetricsRequest()
		{
		}
	}
}