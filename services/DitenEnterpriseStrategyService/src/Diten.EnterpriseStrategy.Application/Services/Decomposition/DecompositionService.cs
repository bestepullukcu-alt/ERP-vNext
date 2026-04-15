using Diten.Application.Common.Interfaces;
using Diten.Application.Dtos.Decomposition;
using Diten.Domain.Aggregates.Decomposition;

namespace Diten.Application.Services.Decomposition;

public sealed class DecompositionService : IDecompositionService
{
    private static readonly Dictionary<string, string[]> AllowedChildren = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Initiative"] = new[] { "Workstream", "Deliverable" },
        ["Workstream"] = new[] { "Work Package", "Deliverable" },
        ["Work Package"] = new[] { "Task" },
        ["Task"] = Array.Empty<string>(),
        ["Deliverable"] = Array.Empty<string>()
    };

    private readonly IRepository<DecompositionStructureAggregate> _repository;

    public DecompositionService(IRepository<DecompositionStructureAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<DecompositionStructureDto> CreateStructureAsync(CreateStructureRequest request, string? actor, CancellationToken ct = default)
    {
        var structure = new DecompositionStructureAggregate
        {
            ParentEntityId = request.ParentEntityId ?? string.Empty,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "New decomposition structure" : request.Name.Trim(),
            StructureType = string.IsNullOrWhiteSpace(request.StructureType) ? "PPM_WBS" : request.StructureType.Trim(),
            Status = "Draft",
            Version = 1,
            CreatedBy = actor
        };
        AddAudit(structure, null, "StructureCreated", $"name={structure.Name}", actor);
        await _repository.AddAsync(structure);
        return MapStructure(structure);
    }

    public async Task<DecompositionStructureDto?> GetStructureAsync(string structureId, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return null;
        RecomputeLevelsAndRollups(structure);
        return MapStructure(structure);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> UpdateStructureAsync(string structureId, UpdateStructureRequest request, string? actor, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");
        if (!string.IsNullOrWhiteSpace(request.Name)) structure.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.StructureType)) structure.StructureType = request.StructureType.Trim();
        Touch(structure, actor);
        AddAudit(structure, null, "StructureUpdated", $"name={structure.Name};type={structure.StructureType}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> CreateNodeAsync(string structureId, CreateNodeRequest request, string? actor, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");
        var type = NormalizeType(request.Type ?? "Initiative");
        if (!IsAllowed(structure, request.ParentId, type, out var err)) return (null, err);

        var node = NewNode(structure, request.ParentId, type, request.Title);
        node.StructureId = structure.Id;
        node.CreatedBy = actor;
        structure.Nodes.Add(node);
        Touch(structure, actor);
        AddAudit(structure, node.Id, "NodeCreated", $"code={node.Code};type={node.Type}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> UpdateNodeAsync(string nodeId, UpdateNodeRequest request, string? actor, CancellationToken ct = default)
    {
        var (structure, node) = await FindByNodeId(nodeId);
        if (structure == null || node == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");

        if (!string.IsNullOrWhiteSpace(request.Type) && !string.Equals(node.Type, request.Type, StringComparison.OrdinalIgnoreCase))
        {
            var targetType = NormalizeType(request.Type);
            if (!IsAllowed(structure, node.ParentId, targetType, out var err)) return (null, err);
            var children = structure.Nodes.Where(x => x.ParentId == node.Id).ToList();
            if (children.Any(c => !AllowedChildren.GetValueOrDefault(targetType, Array.Empty<string>()).Contains(c.Type, StringComparer.OrdinalIgnoreCase)))
                return (null, "InvalidChildTypeForNewParentType");
            node.Type = targetType;
        }

        if (request.Title != null) node.Title = request.Title.Trim();
        if (request.Description != null) node.Description = request.Description.Trim();
        if (request.ResponsibleName != null) node.ResponsibleName = request.ResponsibleName.Trim();
        if (request.DueDate.HasValue) node.DueDate = request.DueDate;
        if (request.Status != null) node.Status = request.Status.Trim();
        if (request.Budget.HasValue) node.Budget = request.Budget;
        if (request.BudgetMode != null) node.BudgetMode = request.BudgetMode.Trim();
        if (request.Metadata != null) node.Metadata = request.Metadata;
        node.LastModifiedBy = actor;
        node.LastModifiedDate = DateTime.UtcNow;
        Touch(structure, actor);
        AddAudit(structure, node.Id, "NodeUpdated", $"code={node.Code}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> DeleteNodeAsync(string nodeId, int expectedVersion, string? actor, CancellationToken ct = default)
    {
        var (structure, node) = await FindByNodeId(nodeId);
        if (structure == null || node == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != expectedVersion) return (null, "VersionConflict");

        var removeIds = Descendants(structure, node.Id);
        removeIds.Add(node.Id);
        structure.Nodes = structure.Nodes.Where(n => !removeIds.Contains(n.Id)).ToList();
        structure.Dependencies = structure.Dependencies.Where(d => !removeIds.Contains(d.FromNodeId) && !removeIds.Contains(d.ToNodeId)).ToList();
        Touch(structure, actor);
        AddAudit(structure, node.Id, "NodeDeleted", $"removed={removeIds.Count}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public Task<(DecompositionStructureDto? dto, string? error)> AddChildAsync(string nodeId, CreateNodeRequest request, string? actor, CancellationToken ct = default) =>
        AddChildOrSibling(nodeId, request, actor, asSibling: false);

    public async Task<(DecompositionStructureDto? dto, string? error)> AddSiblingAsync(string nodeId, AddSiblingRequest request, string? actor, CancellationToken ct = default)
    {
        var converted = new CreateNodeRequest
        {
            ExpectedVersion = request.ExpectedVersion,
            Type = request.Type,
            Title = request.Title
        };
        return await AddChildOrSibling(nodeId, converted, actor, asSibling: true);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> MoveNodeAsync(string nodeId, MoveNodeRequest request, string? actor, CancellationToken ct = default)
    {
        var (structure, node) = await FindByNodeId(nodeId);
        if (structure == null || node == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");

        var targetParentId = request.TargetParentId;
        if (request.PlacementMode.Equals("sibling", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.TargetParentId))
        {
            var pivot = structure.Nodes.FirstOrDefault(x => x.Id == request.TargetParentId);
            targetParentId = pivot?.ParentId;
        }

        if (targetParentId == node.Id) return (null, "InvalidParent");
        if (!IsAllowed(structure, targetParentId, node.Type, out var err)) return (null, err);
        var blocked = Descendants(structure, node.Id);
        if (!string.IsNullOrEmpty(targetParentId) && blocked.Contains(targetParentId)) return (null, "CycleDetected");

        node.ParentId = targetParentId;
        ReorderWithinParent(structure, node.ParentId, node.Id, request.TargetIndex);
        Touch(structure, actor);
        AddAudit(structure, node.Id, "NodeMoved", $"parent={targetParentId};index={request.TargetIndex}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> ReorderNodeAsync(string nodeId, ReorderNodeRequest request, string? actor, CancellationToken ct = default)
    {
        var (structure, node) = await FindByNodeId(nodeId);
        if (structure == null || node == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");
        ReorderWithinParent(structure, node.ParentId, node.Id, request.TargetIndex);
        Touch(structure, actor);
        AddAudit(structure, node.Id, "NodeReordered", $"index={request.TargetIndex}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> AddDependencyAsync(string structureId, AddDependencyRequest request, string? actor, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");

        if (string.IsNullOrWhiteSpace(request.FromNodeId) || string.IsNullOrWhiteSpace(request.ToNodeId)) return (null, "DependencyNodeRequired");
        if (request.FromNodeId == request.ToNodeId) return (null, "DependencySelfReference");
        var from = structure.Nodes.FirstOrDefault(n => n.Id == request.FromNodeId);
        var to = structure.Nodes.FirstOrDefault(n => n.Id == request.ToNodeId);
        if (from == null || to == null) return (null, "DependencyNodeNotFound");
        var exists = structure.Dependencies.Any(d => d.FromNodeId == request.FromNodeId && d.ToNodeId == request.ToNodeId && string.Equals(d.DependencyType, request.DependencyType, StringComparison.OrdinalIgnoreCase));
        if (exists) return (null, "DependencyAlreadyExists");

        structure.Dependencies.Add(new DecompositionDependency
        {
            StructureId = structure.Id,
            FromNodeId = request.FromNodeId,
            ToNodeId = request.ToNodeId,
            DependencyType = string.IsNullOrWhiteSpace(request.DependencyType) ? "FS" : request.DependencyType.Trim()
        });
        Touch(structure, actor);
        AddAudit(structure, null, "DependencyAdded", $"from={from.Code};to={to.Code};type={request.DependencyType}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> DeleteDependencyAsync(string dependencyId, int expectedVersion, string? actor, CancellationToken ct = default)
    {
        var (structure, dependency) = await FindByDependencyId(dependencyId);
        if (structure == null || dependency == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != expectedVersion) return (null, "VersionConflict");
        structure.Dependencies = structure.Dependencies.Where(d => d.Id != dependencyId).ToList();
        Touch(structure, actor);
        AddAudit(structure, null, "DependencyDeleted", $"dependencyId={dependencyId}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> ValidateStructureAsync(string structureId, string? actor, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return (null, "NotFound");
        structure.ValidationIssues = BuildIssues(structure);
        structure.Nodes.ForEach(n => n.ValidationState = structure.ValidationIssues.Any(i => i.NodeId == n.Id && i.Blocking) ? "Invalid" : "Valid");
        Touch(structure, actor);
        AddAudit(structure, null, "StructureValidated", $"issues={structure.ValidationIssues.Count}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<(DecompositionStructureDto? dto, string? error)> ApproveStructureAsync(string structureId, int expectedVersion, string? actor, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != expectedVersion) return (null, "VersionConflict");

        structure.ValidationIssues = BuildIssues(structure);
        if (structure.ValidationIssues.Any(x => x.Blocking))
        {
            var reasons = structure.ValidationIssues.Where(x => x.Blocking).Take(10).Select(x => $"{x.Code}: {x.Message}");
            return (null, $"BlockingIssues:{string.Join("||", reasons)}");
        }

        structure.Status = "Approved";
        structure.ApprovedAt = DateTime.UtcNow;
        structure.ApprovedBy = actor;
        Touch(structure, actor);
        AddAudit(structure, null, "StructureApproved", $"approvedBy={actor}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    public async Task<IReadOnlyList<DecompositionValidationIssueDto>> GetIssuesAsync(string structureId, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return Array.Empty<DecompositionValidationIssueDto>();
        return structure.ValidationIssues
            .OrderByDescending(x => x.Blocking)
            .ThenByDescending(x => x.CreatedDate)
            .Select(MapIssue)
            .ToList();
    }

    public async Task<IReadOnlyList<DecompositionAuditEventDto>> GetHistoryAsync(string structureId, CancellationToken ct = default)
    {
        var structure = await _repository.GetByIdAsync(structureId);
        if (structure == null || structure.IsDeleted) return Array.Empty<DecompositionAuditEventDto>();
        return structure.AuditTrail.OrderByDescending(x => x.CreatedDate).Select(MapAudit).ToList();
    }

    private async Task<(DecompositionStructureAggregate? structure, DecompositionNode? node)> FindByNodeId(string nodeId)
    {
        var all = await _repository.GetAllAsync();
        var structure = all.FirstOrDefault(s => !s.IsDeleted && s.Nodes.Any(n => n.Id == nodeId));
        var node = structure?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        return (structure, node);
    }

    private async Task<(DecompositionStructureAggregate? structure, DecompositionDependency? dependency)> FindByDependencyId(string dependencyId)
    {
        var all = await _repository.GetAllAsync();
        var structure = all.FirstOrDefault(s => !s.IsDeleted && s.Dependencies.Any(d => d.Id == dependencyId));
        var dep = structure?.Dependencies.FirstOrDefault(d => d.Id == dependencyId);
        return (structure, dep);
    }

    private async Task<(DecompositionStructureDto? dto, string? error)> AddChildOrSibling(string nodeId, CreateNodeRequest request, string? actor, bool asSibling)
    {
        var (structure, anchor) = await FindByNodeId(nodeId);
        if (structure == null || anchor == null || structure.IsDeleted) return (null, "NotFound");
        if (structure.Version != request.ExpectedVersion) return (null, "VersionConflict");

        var parentId = asSibling ? anchor.ParentId : anchor.Id;
        var type = NormalizeType(request.Type ?? (asSibling ? anchor.Type : AllowedChildren.GetValueOrDefault(anchor.Type, new[] { "Task" }).FirstOrDefault() ?? "Task"));
        if (!IsAllowed(structure, parentId, type, out var err)) return (null, err);

        var node = NewNode(structure, parentId, type, request.Title);
        node.StructureId = structure.Id;
        node.CreatedBy = actor;
        structure.Nodes.Add(node);
        Touch(structure, actor);
        AddAudit(structure, node.Id, asSibling ? "SiblingAdded" : "ChildAdded", $"parent={parentId};type={type}", actor);
        await _repository.UpdateAsync(structure);
        return (MapStructure(structure), null);
    }

    private static DecompositionNode NewNode(DecompositionStructureAggregate structure, string? parentId, string type, string? title)
    {
        var prefix = type switch
        {
            "Initiative" => "INI",
            "Workstream" => "WKS",
            "Work Package" => "WP",
            "Task" => "TSK",
            "Deliverable" => "DEL",
            _ => "NOD"
        };
        var max = structure.Nodes
            .Select(n => n.Code)
            .Where(c => c.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c[(prefix.Length + 1)..], out var x) ? x : 0)
            .DefaultIfEmpty(0)
            .Max();
        var siblings = structure.Nodes.Where(n => n.ParentId == parentId).ToList();
        return new DecompositionNode
        {
            ParentId = parentId,
            Type = type,
            Title = string.IsNullOrWhiteSpace(title) ? $"New {type}" : title.Trim(),
            Code = $"{prefix}-{(max + 1):D3}",
            Status = "Draft",
            SortOrder = siblings.Count + 1
        };
    }

    private static bool IsAllowed(DecompositionStructureAggregate structure, string? parentId, string childType, out string? err)
    {
        err = null;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            if (!childType.Equals("Initiative", StringComparison.OrdinalIgnoreCase))
            {
                err = "RootMustBeInitiative";
                return false;
            }
            return true;
        }
        var parent = structure.Nodes.FirstOrDefault(n => n.Id == parentId);
        if (parent == null)
        {
            err = "ParentNotFound";
            return false;
        }
        var allowed = AllowedChildren.GetValueOrDefault(parent.Type, Array.Empty<string>());
        if (!allowed.Contains(childType, StringComparer.OrdinalIgnoreCase))
        {
            err = "ParentChildRuleViolation";
            return false;
        }
        return true;
    }

    private static void ReorderWithinParent(DecompositionStructureAggregate structure, string? parentId, string nodeId, int targetIndex)
    {
        var siblings = structure.Nodes.Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder).ToList();
        var node = siblings.First(x => x.Id == nodeId);
        siblings.Remove(node);
        var idx = targetIndex < 0 ? siblings.Count : Math.Min(targetIndex, siblings.Count);
        siblings.Insert(idx, node);
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i + 1;
    }

    private static HashSet<string> Descendants(DecompositionStructureAggregate structure, string rootId)
    {
        var outSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string id)
        {
            foreach (var c in structure.Nodes.Where(x => x.ParentId == id))
            {
                if (outSet.Add(c.Id)) Walk(c.Id);
            }
        }
        Walk(rootId);
        return outSet;
    }

    private static string NormalizeType(string input)
    {
        var t = input.Trim();
        if (t.Equals("WorkPackage", StringComparison.OrdinalIgnoreCase)) return "Work Package";
        return t;
    }

    private static List<DecompositionValidationIssue> BuildIssues(DecompositionStructureAggregate structure)
    {
        var issues = new List<DecompositionValidationIssue>();
        var now = DateTime.UtcNow.Date;
        var nodeMap = structure.Nodes.ToDictionary(n => n.Id, n => n);
        foreach (var n in structure.Nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Title))
                issues.Add(Issue(structure.Id, n.Id, "Error", "NODE_TITLE_REQUIRED", "Title is required.", true));
            if (string.IsNullOrWhiteSpace(n.ResponsibleName))
                issues.Add(Issue(structure.Id, n.Id, "Warning", "NODE_OWNER_REQUIRED", "Responsible owner is missing.", false));
            if (!n.DueDate.HasValue)
                issues.Add(Issue(structure.Id, n.Id, "Warning", "NODE_DUE_REQUIRED", "Due date is missing.", false));
            if (n.DueDate.HasValue && n.DueDate.Value.Date < now && !string.Equals(n.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                issues.Add(Issue(structure.Id, n.Id, "Warning", "NODE_OVERDUE", "Node due date is overdue.", false));
            if (!string.IsNullOrWhiteSpace(n.ParentId))
            {
                var parent = structure.Nodes.FirstOrDefault(x => x.Id == n.ParentId);
                if (parent == null)
                    issues.Add(Issue(structure.Id, n.Id, "Error", "PARENT_NOT_FOUND", "Parent node not found.", true));
                else if (!AllowedChildren.GetValueOrDefault(parent.Type, Array.Empty<string>()).Contains(n.Type, StringComparer.OrdinalIgnoreCase))
                    issues.Add(Issue(structure.Id, n.Id, "Error", "PARENT_CHILD_RULE", $"Type {n.Type} is not allowed under {parent.Type}.", true));
            }
        }
        var duplicateCodes = structure.Nodes.GroupBy(x => x.Code).Where(g => g.Count() > 1).ToList();
        foreach (var dup in duplicateCodes)
            issues.Add(Issue(structure.Id, null, "Error", "CODE_DUPLICATE", $"Duplicate node code detected: {dup.Key}", true));

        foreach (var dep in structure.Dependencies)
        {
            if (!nodeMap.ContainsKey(dep.FromNodeId) || !nodeMap.ContainsKey(dep.ToNodeId))
                issues.Add(Issue(structure.Id, null, "Error", "DEPENDENCY_NODE_MISSING", $"Dependency {dep.Id} references unknown nodes.", true));
            if (dep.FromNodeId == dep.ToNodeId)
                issues.Add(Issue(structure.Id, dep.FromNodeId, "Error", "DEPENDENCY_SELF", "Node cannot depend on itself.", true));
        }

        if (HasHierarchyCycle(structure))
            issues.Add(Issue(structure.Id, null, "Error", "HIERARCHY_CYCLE", "Hierarchy cycle detected.", true));

        if (HasDependencyCycle(structure))
            issues.Add(Issue(structure.Id, null, "Error", "DEPENDENCY_CYCLE", "Dependency cycle detected.", true));

        if (!structure.Nodes.Any())
            issues.Add(Issue(structure.Id, null, "Warning", "EMPTY_STRUCTURE", "Structure has no nodes.", false));
        return issues;
    }

    private static bool HasHierarchyCycle(DecompositionStructureAggregate structure)
    {
        var byId = structure.Nodes.ToDictionary(x => x.Id, x => x);
        foreach (var n in structure.Nodes)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = n;
            while (!string.IsNullOrWhiteSpace(current.ParentId) && byId.TryGetValue(current.ParentId, out var p))
            {
                if (!seen.Add(p.Id)) return true;
                current = p;
            }
        }
        return false;
    }

    private static bool HasDependencyCycle(DecompositionStructureAggregate structure)
    {
        var graph = structure.Dependencies
            .GroupBy(d => d.FromNodeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ToNodeId).ToList(), StringComparer.OrdinalIgnoreCase);

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Dfs(string id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            if (graph.TryGetValue(id, out var next))
            {
                foreach (var n in next)
                    if (Dfs(n)) return true;
            }
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        foreach (var n in structure.Nodes.Select(x => x.Id))
            if (Dfs(n)) return true;
        return false;
    }

    private static DecompositionValidationIssue Issue(string sid, string? nodeId, string severity, string code, string message, bool blocking) =>
        new()
        {
            StructureId = sid,
            NodeId = nodeId,
            Severity = severity,
            Code = code,
            Message = message,
            Blocking = blocking
        };

    private static void RecomputeLevelsAndRollups(DecompositionStructureAggregate structure)
    {
        var map = structure.Nodes.ToDictionary(x => x.Id, x => x);
        foreach (var n in structure.Nodes)
        {
            var level = 0;
            var parentId = n.ParentId;
            while (!string.IsNullOrWhiteSpace(parentId) && map.TryGetValue(parentId, out var p))
            {
                level++;
                parentId = p.ParentId;
                if (level > 50) break;
            }
            n.Level = level;
        }
        foreach (var n in structure.Nodes)
        {
            var rollup = structure.Nodes.Where(x => x.ParentId == n.Id).Sum(x => x.Budget ?? 0);
            n.ChildRollupBudget = rollup;
        }
        structure.Nodes = structure.Nodes.OrderBy(x => x.Level).ThenBy(x => x.ParentId).ThenBy(x => x.SortOrder).ToList();
    }

    private static DecompositionStructureDto MapStructure(DecompositionStructureAggregate structure)
    {
        RecomputeLevelsAndRollups(structure);
        var issues = structure.ValidationIssues ?? new List<DecompositionValidationIssue>();
        var blocking = issues.Count(x => x.Blocking);
        var warning = issues.Count(x => !x.Blocking);
        var readiness = ComputeReadinessPercent(structure, issues);
        return new DecompositionStructureDto
        {
            Id = structure.Id,
            ParentEntityId = structure.ParentEntityId,
            Name = structure.Name,
            StructureType = structure.StructureType,
            Status = structure.Status,
            Version = structure.Version,
            ApprovedAt = structure.ApprovedAt,
            ApprovedBy = structure.ApprovedBy,
            Nodes = structure.Nodes.Select(MapNode).ToList(),
            Dependencies = structure.Dependencies.Select(MapDependency).ToList(),
            ValidationSummary = new DecompositionValidationSummaryDto
            {
                TotalIssues = issues.Count,
                BlockingIssues = blocking,
                WarningIssues = warning,
                ReadinessPercent = readiness
            }
        };
    }

    private static int ComputeReadinessPercent(DecompositionStructureAggregate structure, IReadOnlyList<DecompositionValidationIssue> issues)
    {
        if (!structure.Nodes.Any()) return 0;
        var penalties = issues.Count(i => i.Blocking ? true : i.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var raw = Math.Max(0, 100 - (penalties * 5));
        return Math.Clamp(raw, 0, 100);
    }

    private static DecompositionNodeDto MapNode(DecompositionNode n) => new()
    {
        Id = n.Id,
        ParentId = n.ParentId,
        Code = n.Code,
        Title = n.Title,
        Type = n.Type,
        Description = n.Description,
        ResponsibleName = n.ResponsibleName,
        DueDate = n.DueDate,
        Status = n.Status,
        Budget = n.Budget,
        BudgetMode = n.BudgetMode,
        ChildRollupBudget = n.ChildRollupBudget,
        SortOrder = n.SortOrder,
        Level = n.Level,
        ValidationState = n.ValidationState,
        Metadata = n.Metadata
    };

    private static DecompositionDependencyDto MapDependency(DecompositionDependency d) => new()
    {
        Id = d.Id,
        FromNodeId = d.FromNodeId,
        ToNodeId = d.ToNodeId,
        DependencyType = d.DependencyType
    };

    private static DecompositionValidationIssueDto MapIssue(DecompositionValidationIssue i) => new()
    {
        Id = i.Id,
        NodeId = i.NodeId,
        Severity = i.Severity,
        Code = i.Code,
        Message = i.Message,
        Blocking = i.Blocking,
        CreatedAt = i.CreatedDate
    };

    private static DecompositionAuditEventDto MapAudit(DecompositionAuditEvent e) => new()
    {
        Id = e.Id,
        NodeId = e.NodeId,
        EventType = e.EventType,
        EventPayload = e.EventPayload,
        Actor = e.Actor,
        CreatedAt = e.CreatedDate
    };

    private static void Touch(DecompositionStructureAggregate structure, string? actor)
    {
        structure.Version += 1;
        structure.LastModifiedBy = actor;
        structure.LastModifiedDate = DateTime.UtcNow;
    }

    private static void AddAudit(DecompositionStructureAggregate structure, string? nodeId, string type, string payload, string? actor)
    {
        structure.AuditTrail.Add(new DecompositionAuditEvent
        {
            StructureId = structure.Id,
            NodeId = nodeId,
            EventType = type,
            EventPayload = payload,
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor
        });
    }
}
