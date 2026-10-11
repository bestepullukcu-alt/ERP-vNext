#!/usr/bin/env python3
"""SHIPMENT-BUNDLE contract self-consistency walker.

WHY THIS IS A SIBLING OF g5_golden_flow.py AND NOT AN EXTENSION OF IT
    g5_golden_flow.py's conform() answers "does the RUNNING service match the contract".
    It needs a gateway, a tenant, a legal entity and MongoDB, and it checks a hardcoded
    dozen responses. This file answers a different question that needs none of that:
    "is the contract consistent with ITSELF" -- every declared example validated against
    the schema declared beside it. Folding it into the live runner would make a check that
    needs no stack unrunnable without one.

WHAT IT CLOSES
    Three acceptance lines stay open for the same reason -- "all examples and schemas" was
    never exhaustively checked:
      MOD-0184-carrier-management.md:119
      MOD-0185-routing-load-planning.md:125
      MOD-0187-claims-management.md:122
    This walker is the exhaustive half. It does not touch runtime behaviour.

FAIL-CLOSED, BY DESIGN
    The live runner prints a note and increments neither ok nor bad for an undeclared
    status, a missing declared body or an unresolvable path, so it can exit 0 while the
    contract is violated. Every condition here lands in a counter and moves the exit code.
    A walker that cannot fail is not a walker.

EXIT CODE IS A BITMASK, so one run reports every class of problem at once:
    0  clean
    1  an example failed the schema declared beside it   (contract defect)
    2  a structural defect (unresolvable $ref, external $ref, declared body with no schema)
    4  enumeration mismatch (operation/webhook census differs from expectation)
    8  could not load or parse the contract
"""

import hashlib
import json
import os
import sys

HTTP_METHODS = ("get", "put", "post", "delete", "patch", "options", "head", "trace")
IN_SCOPE_FAMILIES = ("carriers", "loads", "claims")
EXPECTED_IN_SCOPE_OPS = 9          # Carrier 3 + Load 3 + Claim 3; asserted, not assumed
EXPECTED_WEBHOOKS = 1              # shipmentLifecycleEvent

EXIT_SCHEMA_VIOLATION = 1
EXIT_STRUCTURAL = 2
EXIT_ENUMERATION = 4
EXIT_LOAD = 8

DEFAULT_CONTRACT = "docs/analysis/contracts/shipment-bundle.openapi.yaml"


def esc(token):
    """JSON Pointer escaping (RFC 6901). '/' and '~' must be encoded or the pointer breaks."""
    return token.replace("~", "~0").replace("/", "~1")


class Walker(object):
    def __init__(self, spec, path):
        self.spec = spec
        self.path = path
        self.violations = []      # example contradicts its own schema -> contract defect
        self.structural = []      # unresolvable/external $ref, declared body with no schema
        self.enumeration = []     # census disagreements
        self.gaps = []            # schema declared, zero examples -> nothing to check
        self.unvalidatable = []   # externalValue examples; cannot be read offline
        self.validated = 0
        self.distinct = set()     # (schema_pointer, example_pointer) -- shared $ref'd
                                  # component examples are reachable from many operations;
                                  # counting the validations hides how many objects exist
        self.headers = {}         # operationId -> [(name, required, source)]
        self.error_codes = {}     # operationId -> [(status, example name, error.code)]
        self._build_registry()

    # ---------------------------------------------------------------- resolver
    def _build_registry(self):
        """Register the whole document under one URI so that '#/...' inside any subschema
        resolves against the DOCUMENT, not against the subschema being validated. Validating
        an inline schema dict directly makes its own local refs unresolvable -- measured."""
        from referencing import Registry, Resource
        from referencing.jsonschema import DRAFT202012
        self._resource_uri = "urn:shipment-bundle"
        self._registry = Registry().with_resource(
            self._resource_uri, Resource(contents=self.spec, specification=DRAFT202012))

    def resolve(self, node, pointer, chain=0):
        """Follow $ref chains inside the document. Returns (node, pointer) of the target so
        the caller can both READ it and point a validator at it. External refs are refused
        rather than silently skipped: an unresolvable reference is a structural defect."""
        while isinstance(node, dict) and "$ref" in node:
            if chain > 32:
                self.structural.append(("ref-cycle", pointer, node.get("$ref")))
                return None, None
            ref = node["$ref"]
            if not ref.startswith("#/"):
                self.structural.append(("external-ref", pointer, ref))
                return None, None
            target = self.spec
            parts = ref[2:].split("/")
            for part in parts:
                part = part.replace("~1", "/").replace("~0", "~")
                if not isinstance(target, dict) or part not in target:
                    self.structural.append(("unresolvable-ref", pointer, ref))
                    return None, None
                target = target[part]
            node = target
            pointer = "/" + "/".join(esc(p.replace("~1", "/").replace("~0", "~")) for p in parts)
            chain += 1
        return node, pointer

    def validate(self, instance, schema_pointer, label, example_pointer=None):
        """Validate one example against the schema declared beside it, with format checking
        ON. Without a format_checker 'format: uuid' and 'format: date-time' are inert, which
        is how a contract can declare a uuid and accept 'nope'."""
        from jsonschema import Draft202012Validator
        validator = Draft202012Validator(
            {"$ref": self._resource_uri + "#" + schema_pointer},
            registry=self._registry,
            format_checker=Draft202012Validator.FORMAT_CHECKER)
        errors = sorted(validator.iter_errors(instance), key=lambda e: list(e.path))
        if errors:
            first = errors[0]
            where = "/".join(str(p) for p in first.path) or "(root)"
            self.violations.append((label, schema_pointer, where, first.message))
            return False
        self.validated += 1
        if example_pointer:
            self.distinct.add((schema_pointer, example_pointer))
        return True

    # -------------------------------------------------------------- traversal
    def examples_at(self, media, media_pointer):
        """Both declaration forms, because the contract uses both: a single inline `example`
        and a named `examples` map. A walker that reads only one form reports a clean run
        over half the evidence."""
        found = []
        if isinstance(media, dict):
            if "example" in media:
                found.append(("example", media["example"], media_pointer + "/example"))
            named = media.get("examples")
            if isinstance(named, dict):
                for name, holder in named.items():
                    holder, holder_ptr = self.resolve(
                        holder, media_pointer + "/examples/" + esc(name))
                    if holder is None:
                        continue
                    if isinstance(holder, dict) and "externalValue" in holder:
                        self.unvalidatable.append((media_pointer, name, holder["externalValue"]))
                        continue
                    if isinstance(holder, dict) and "value" in holder:
                        found.append((name, holder["value"], holder_ptr + "/value"))
                    else:
                        found.append((name, holder, holder_ptr))
        return found

    def walk_media(self, container, container_pointer, label, expect_schema):
        """One `content` block: resolve its schema, then validate every example against it."""
        content = container.get("content") if isinstance(container, dict) else None
        if not content:
            if expect_schema:
                self.structural.append(("declared-without-body", container_pointer, label))
            return
        for media_type, media in content.items():
            media_pointer = container_pointer + "/content/" + esc(media_type)
            schema = media.get("schema") if isinstance(media, dict) else None
            if schema is None:
                self.structural.append(("content-without-schema", media_pointer, label))
                continue
            _, schema_pointer = self.resolve(schema, media_pointer + "/schema")
            if schema_pointer is None:
                continue
            examples = self.examples_at(media, media_pointer)
            if not examples:
                self.gaps.append((label, media_pointer))
                continue
            for name, value, example_pointer in examples:
                self.validate(value, schema_pointer, label + " :: " + name, example_pointer)

    def collect_headers(self, op, op_pointer, operation_id, path_level_params):
        """Required request headers, inline AND $ref'd. Loads declares X-Tenant-Id and
        X-Legal-Entity-Id inline while Carriers and Claims use components/parameters, so a
        walker keyed only on components misses three operations' worth of headers."""
        rows = []
        params = list(path_level_params) + list(op.get("parameters") or [])
        for index, raw in enumerate(params):
            resolved, _ = self.resolve(raw, op_pointer + "/parameters/" + str(index))
            if not isinstance(resolved, dict):
                continue
            if resolved.get("in") != "header":
                continue
            source = "$ref" if isinstance(raw, dict) and "$ref" in raw else "inline"
            rows.append((resolved.get("name"), bool(resolved.get("required")), source))
        self.headers[operation_id] = rows

    def collect_error_codes(self, status, examples, operation_id):
        """Error example codes as data. The live comparison is a later WP; this produces the
        table it will compare against."""
        if not (status.startswith("4") or status.startswith("5")):
            return
        for name, value, _ in examples:
            code = None
            if isinstance(value, dict):
                err = value.get("error")
                if isinstance(err, dict):
                    code = err.get("code")
            self.error_codes.setdefault(operation_id, []).append((status, name, code))

    def run(self):
        spec = self.spec
        paths = spec.get("paths") or {}
        webhooks = spec.get("webhooks") or {}

        operations = []
        for path, item in paths.items():
            item, _ = self.resolve(item, "/paths/" + esc(path))
            if not isinstance(item, dict):
                continue
            path_params = item.get("parameters") or []
            for method, op in item.items():
                if method not in HTTP_METHODS or not isinstance(op, dict):
                    continue
                operations.append((path, method, op, path_params))

        # ---- census, enumerated rather than hardcoded
        def family(p):
            for f in ("shipments", "carriers", "loads", "returns", "claims"):
                if p.startswith("/" + f):
                    return f
            return "?"

        in_scope = [o for o in operations if family(o[0]) in IN_SCOPE_FAMILIES]
        status_total = sum(len(o[2].get("responses") or {}) for o in operations)
        status_in_scope = sum(len(o[2].get("responses") or {}) for o in in_scope)

        print("CONTRACT  %s %s" % (spec["info"]["title"], spec["info"]["version"]))
        print("FILE      %s" % self.path)
        print("SHA-256   %s" % hashlib.sha256(
            open(self.path, "rb").read()).hexdigest())
        print()
        print("ENUMERATION (walked, not hardcoded)")
        print("  paths                      %d" % len(paths))
        print("  operations (all families)  %d" % len(operations))
        print("  status codes (all)         %d" % status_total)
        print("  in-scope operations        %d   [%s]" % (
            len(in_scope), ", ".join(IN_SCOPE_FAMILIES)))
        print("  in-scope status codes      %d" % status_in_scope)
        print("  webhooks                   %d   %s" % (
            len(webhooks), ", ".join(sorted(webhooks)) or "-"))
        if len(in_scope) != EXPECTED_IN_SCOPE_OPS:
            self.enumeration.append(
                ("in-scope operation count", EXPECTED_IN_SCOPE_OPS, len(in_scope)))
        if len(webhooks) != EXPECTED_WEBHOOKS:
            self.enumeration.append(("webhook count", EXPECTED_WEBHOOKS, len(webhooks)))
        print()

        print("OPERATIONS")
        for path, method, op, _ in operations:
            mark = "*" if family(path) in IN_SCOPE_FAMILIES else " "
            print("  %s%-6s %-34s %-26s %d codes" % (
                mark, method.upper(), path, op.get("operationId"),
                len(op.get("responses") or {})))
        print("  (* = in scope for MOD-0184 / MOD-0185 / MOD-0187)")
        print()

        # ---- every example against the schema beside it
        print("EXAMPLE VALIDATION")
        for path, method, op, path_params in operations:
            operation_id = op.get("operationId") or (method + " " + path)
            op_pointer = "/paths/" + esc(path) + "/" + method
            self.collect_headers(op, op_pointer, operation_id, path_params)

            body = op.get("requestBody")
            if body is not None:
                resolved, body_pointer = self.resolve(body, op_pointer + "/requestBody")
                if resolved is not None:
                    self.walk_media(resolved, body_pointer,
                                    operation_id + " requestBody", expect_schema=True)

            for status, response in (op.get("responses") or {}).items():
                resolved, resp_pointer = self.resolve(
                    response, op_pointer + "/responses/" + esc(str(status)))
                if resolved is None:
                    continue
                content = resolved.get("content") if isinstance(resolved, dict) else None
                if content:
                    for media_type, media in content.items():
                        media_pointer = resp_pointer + "/content/" + esc(media_type)
                        self.collect_error_codes(
                            str(status), self.examples_at(media, media_pointer), operation_id)
                # 204 and friends legitimately carry no body; only flag a missing body when
                # the response declares content it then fails to describe.
                self.walk_media(resolved, resp_pointer,
                                "%s %s" % (operation_id, status),
                                expect_schema=False)

        for name, webhook in webhooks.items():
            webhook, wh_pointer = self.resolve(webhook, "/webhooks/" + esc(name))
            if not isinstance(webhook, dict):
                continue
            for method, op in webhook.items():
                if method not in HTTP_METHODS or not isinstance(op, dict):
                    continue
                body = op.get("requestBody")
                if body is not None:
                    resolved, body_pointer = self.resolve(body, wh_pointer + "/" + method + "/requestBody")
                    if resolved is not None:
                        self.walk_media(resolved, body_pointer,
                                        "webhook:" + name, expect_schema=True)

        print("  validations performed      %d   (an example shared by N operations counts N times)"
              % self.validated)
        print("  DISTINCT example objects  %d   (unique schema+example pointer pairs)"
              % len(self.distinct))
        print("  examples VIOLATING schema  %d" % len(self.violations))
        for label, pointer, where, message in self.violations:
            print("    ! %s" % label)
            print("      at %s" % pointer)
            print("      field %s -- %s" % (where, message[:140]))
        print()

        print("STRUCTURAL DEFECTS  %d" % len(self.structural))
        for kind, pointer, detail in self.structural:
            print("  ! %-24s %s  (%s)" % (kind, pointer, detail))
        if self.unvalidatable:
            print("  externalValue examples (cannot be read offline): %d" % len(self.unvalidatable))
            for pointer, name, uri in self.unvalidatable:
                print("    - %s :: %s -> %s" % (pointer, name, uri))
        print()

        print("COVERAGE GAPS -- schema declared, zero examples  %d" % len(self.gaps))
        for label, pointer in self.gaps:
            print("  . %-34s %s" % (label, pointer))
        print()

        print("REQUIRED REQUEST HEADERS (inline and $ref'd)")
        for operation_id in sorted(self.headers):
            rows = [r for r in self.headers[operation_id] if r[1]]
            if not rows:
                continue
            print("  %-26s %s" % (operation_id, ", ".join(
                "%s[%s]" % (n, s) for n, _, s in rows)))
        print()

        print("ERROR EXAMPLE CODES")
        for operation_id in sorted(self.error_codes):
            seen = []
            for status, name, code in self.error_codes[operation_id]:
                token = "%s=%s" % (status, code if code else "(no error.code)")
                if token not in seen:
                    seen.append(token)
            print("  %-26s %s" % (operation_id, " ".join(seen)))
        print()

        self.negative_checks()

        total = len(self.violations) + len(self.structural) + len(self.enumeration)
        print("SUMMARY")
        print("  validations %d · distinct examples %d · violations %d · structural %d · "
              "enumeration %d · gaps %d"
              % (self.validated, len(self.distinct), len(self.violations),
                 len(self.structural), len(self.enumeration), len(self.gaps)))
        for what, expected, actual in self.enumeration:
            print("  ! enumeration mismatch: %s expected %s, walked %s" % (what, expected, actual))

        code = 0
        if self.violations:
            code |= EXIT_SCHEMA_VIOLATION
        if self.structural:
            code |= EXIT_STRUCTURAL
        if self.enumeration:
            code |= EXIT_ENUMERATION
        print("  exit %d%s" % (code, "" if code else "  (clean)"))
        return code

    def negative_checks(self):
        """MOD-0184:119 obliges a Carrier EVENT. The frozen event vocabulary has no Carrier
        value, so the obligation can only be discharged as a NEGATIVE assertion against the
        contract file. Asserted here so the absence is measured rather than assumed."""
        print("NEGATIVE CHECKS")
        schemas = (self.spec.get("components") or {}).get("schemas") or {}

        def find_enum(name_fragment, field):
            for schema_name, schema in schemas.items():
                props = schema.get("properties") if isinstance(schema, dict) else None
                if not isinstance(props, dict):
                    continue
                target = props.get(field)
                if isinstance(target, dict) and isinstance(target.get("enum"), list):
                    return schema_name, target["enum"]
            return None, None

        holder, event_types = find_enum("event", "eventType")
        holder2, aggregates = find_enum("event", "aggregateType")
        for field, holder_name, values in (("eventType", holder, event_types),
                                           ("aggregateType", holder2, aggregates)):
            if values is None:
                print("  ? %s enum not found -- cannot assert Carrier absence" % field)
                self.structural.append(("enum-not-found", "/components/schemas", field))
                continue
            carrier_values = [v for v in values if "carrier" in str(v).lower()]
            verdict = "ABSENT (as the pack expects)" if not carrier_values else \
                      "PRESENT: %s" % carrier_values
            print("  %s Carrier in %s.%s -> %s" % (
                "." if not carrier_values else "!", holder_name, field, verdict))
            if carrier_values:
                self.violations.append(
                    ("carrier-event-unexpectedly-present", "/components/schemas/%s" % holder_name,
                     field, "pack MOD-0184:119 is written against a contract with no Carrier event"))
        print()


def main():
    path = os.environ.get("G5_CONTRACT", DEFAULT_CONTRACT)
    if not os.path.exists(path):
        print("cannot read contract: %s" % path, file=sys.stderr)
        return EXIT_LOAD
    try:
        import yaml
        spec = yaml.safe_load(open(path))
    except Exception as exc:                                  # noqa: BLE001
        print("cannot parse contract: %s" % exc, file=sys.stderr)
        return EXIT_LOAD
    if not isinstance(spec, dict) or "paths" not in spec:
        print("contract has no paths object", file=sys.stderr)
        return EXIT_LOAD
    return Walker(spec, path).run()


if __name__ == "__main__":
    sys.exit(main())
