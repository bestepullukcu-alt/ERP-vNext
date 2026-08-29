const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

describe('MOD-0354 legacy parity containment', () => {
    const root = path.join(__dirname, '..');
    const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
    const sha = (relative) => crypto.createHash('sha256').update(fs.readFileSync(path.join(root, relative))).digest('hex');

    it('keeps the legacy controller and page byte-identical to the recorded baseline', () => {
        expect(sha('Controllers/DecompositionTreeBuilderController.cs')).toBe('580dbb3bfd5ef2088a77beeb918439da7045730048e2c77bafdcaf082e8d2317');
        expect(sha('Views/DecompositionTreeBuilder/Index.cshtml')).toBe('6b85f9dd3b9753a891cc9b851c5f3f47fecbce61ab34774713cb794e4f9fd8ea');
    });

    it('records every required parity dimension without claiming retirement', () => {
        const evidence = read('../../execution/domains/management-governance/evidence/MOD-0354-legacy-parity.md');
        ['Old URL', 'Old entities', 'Fields', 'Commands/actions', 'Lifecycle', 'Permissions', 'Integrations',
         'New owner/module', 'New URL', 'Field mapping', 'Disposition', 'Migration requirement', 'Browser evidence']
            .forEach((heading) => expect(evidence).toContain(heading));
        expect(evidence).toContain('Out of Scope');
        expect(evidence).toContain('Intentionally Removed');
        expect(evidence).toContain('PENDING USER ACCEPTANCE');
        expect(evidence).not.toContain('FULL PARITY COMPLETE');
    });

    it('does not copy legacy task, owner, schedule, status or approval controls into DWS', () => {
        const modern = read('Views/ManagementGovernance/Dws/Index.cshtml');
        expect(modern).not.toMatch(/decomp-(?:approval|owner|due|status)|approve|assign|escalate|progress/i);
    });
});
