'use strict';
(function () {


    const dtRecievedDate = document.querySelector('#dt-received');
    if (dtRecievedDate) {
        dtRecievedDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }
    const dtEntry = document.querySelector('#dt-entry');
    if (dtEntry) {
        dtEntry.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }
    const dtDueDate = document.querySelector('#dt-due');
    if (dtDueDate) {
        dtDueDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    const dtSubmissionDueDate = document.querySelector('#dt-submission-due');
    if (dtSubmissionDueDate) {
        dtSubmissionDueDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    const dtSubmissionDueDateMin = document.querySelector('#dt-safety-submission-due-min');
    if (dtSubmissionDueDateMin) {
        dtSubmissionDueDateMin.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    const dtSubmissionDueDateMax = document.querySelector('#dt-safety-submission-due-max');
    if (dtSubmissionDueDateMax) {
        dtSubmissionDueDateMax.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    const dtSubmissionDateMin = document.querySelector('#dt-safety-submission-min');
    if (dtSubmissionDateMin) {
        dtSubmissionDateMin.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    const dtSubmissionDateMax = document.querySelector('#dt-safety-submission-max');
    if (dtSubmissionDateMax) {
        dtSubmissionDateMax.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

    window.summaryEditor = new Quill('#summary-editor', {
        bounds: '#summary-editor',
        modules: {
            syntax: true,
            toolbar: '#summary-toolbar'
        },
        theme: 'snow'
    });

    window.safetyCommentEditor = new Quill('#safety-comment-editor', {
        bounds: '#safety-comment-editor',
        modules: {
            syntax: true,
            toolbar: '#safety-comment-toolbar'
        },
        theme: 'snow'
    });

    window.assessmentCommentEditor = new Quill('#assessment-comment-editor', {
        bounds: '#assessment-comment-editor',
        modules: {
            syntax: true,
            toolbar: '#assessment-comment-toolbar'
        },
        theme: 'snow'
    });

})();
