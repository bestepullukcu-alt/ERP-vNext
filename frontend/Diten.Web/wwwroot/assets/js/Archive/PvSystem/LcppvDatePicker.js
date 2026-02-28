'use strict';
(function () {


    const dtStartDate = document.querySelector('#dtStartDate');
    if (dtStartDate) {
        dtStartDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }
    const dtEndDate = document.querySelector('#dtEndDate');
    if (dtEndDate) {
        dtEndDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }
    const dtDueDate = document.querySelector('#dtDueDate');
    if (dtDueDate) {
        dtDueDate.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }


    window.commentEditor = new Quill('#comment-editor', {
        bounds: '#comment-editor',
        modules: {
            syntax: true,
            toolbar: '#comment-toolbar'
        },
        theme: 'snow'
    });

})();
