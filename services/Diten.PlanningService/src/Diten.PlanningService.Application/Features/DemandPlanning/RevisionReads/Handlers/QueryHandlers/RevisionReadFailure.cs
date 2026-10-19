namespace Diten.PlanningService.Application.Features.DemandPlanning;

internal static class RevisionReadFailure
{
    public static Response<T> FromSnapshot<T>(SnapshotReadOutcome outcome) =>
        outcome switch
        {
            SnapshotReadOutcome.NotFound => Response<T>.Fail("Revision not found.", 404),
            SnapshotReadOutcome.PermissionDenied => Response<T>.Fail("Demand permission is required.", 403),
            SnapshotReadOutcome.StateDenied => Response<T>.Fail("Revision state does not permit this read.", 409),
            SnapshotReadOutcome.InvalidCursor => Response<T>.Fail("Cursor or page size is invalid.", 409),
            _ => Response<T>.Fail("Authoritative read or integrity verification is unavailable.", 503)
        };

    public static Response<T> FromStatus<T>(AuthoritativeStatusOutcome outcome) =>
        outcome switch
        {
            AuthoritativeStatusOutcome.NotFound => Response<T>.Fail("Revision not found.", 404),
            AuthoritativeStatusOutcome.PermissionDenied => Response<T>.Fail("Demand permission is required.", 403),
            _ => Response<T>.Fail("Authoritative revision status is unavailable.", 503)
        };
}
