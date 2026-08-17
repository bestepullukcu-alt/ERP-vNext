# Interface Registry API

Module: MOD-0002  
Gateway base path: `/api/platform/interface-registry`  
Frontend surface: `/Platform/InterfaceRegistry`

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/platform/interface-registry/manifests/import` | Import an interface manifest document. |
| GET | `/api/platform/interface-registry/discovery-batches` | List discovery batches. |
| GET | `/api/platform/interface-registry/discovery-batches/{batchId}` | Get a discovery batch. |
| GET | `/api/platform/interface-registry/discovery-batches/{batchId}/diffs` | List diffs for a discovery batch. |
| GET | `/api/platform/interface-registry/interfaces` | List interface definitions. |
| GET | `/api/platform/interface-registry/interfaces/{interfaceCode}/snapshot?version=` | Get an active interface snapshot. |
| POST | `/api/platform/interface-registry/discovery-batches/{batchId}/confirm` | Confirm a discovery batch. |
| POST | `/api/platform/interface-registry/discovery-batches/{batchId}/reject` | Reject a discovery batch with a reason. |
| POST | `/api/platform/interface-registry/diffs/{diffItemId}/confirm` | Confirm a diff item. |
| POST | `/api/platform/interface-registry/diffs/{diffItemId}/reject` | Reject a diff item with a reason. |
| POST | `/api/platform/interface-registry/interfaces/{interfaceCode}/deprecate` | Deprecate an interface version. |

## Permissions

The controller uses `Platform.InterfaceRegistry.Import`, `Read`, `Review`, and `Deprecate` permission keys.

