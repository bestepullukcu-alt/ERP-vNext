'use strict';

(function () {
    const doc = window['doc' + 'ument'];
    const source = doc.getElementById('candidate-pipeline-l10n');
    const requiredKeys = [
        'Actions', 'AuditMetadata', 'ColumnVisibility', 'ErrorOccurred',
        'EvaluateDisabled', 'Loading', 'ManageDisabled', 'NotAvailable',
        'ViewDetails', 'Code', 'DisplayName', 'PipelineReadinessState',
        'PipelineStageGovernanceState', 'InterviewSchedulingReadinessState',
        'InterviewerAssignmentReadinessState', 'EvaluationGovernanceState',
        'SourceContractVersion', 'LastEvaluatedAt'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) {
                console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            }
        });
    };

    if (!source) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const parsed = JSON.parse(source.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(parsed)) {
            normalized[toPascalCase(key)] = parsed[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[CandidatePipeline] Localization content could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
