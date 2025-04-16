#nullable enable
#pragma warning disable CS8618
using EdjCase.ICP.Candid.Mapping;

namespace Cosmicrafts.backend.Models
{
	public enum PrivacySetting
	{
		[CandidName("acceptAll")]
		AcceptAll,
		[CandidName("blockAll")]
		BlockAll,
		[CandidName("friendsOfFriends")]
		FriendsOfFriends
	}
}