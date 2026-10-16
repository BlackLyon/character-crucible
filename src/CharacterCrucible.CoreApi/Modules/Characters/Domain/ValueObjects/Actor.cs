using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

public record Actor(Guid Id, ActorRole Role);
