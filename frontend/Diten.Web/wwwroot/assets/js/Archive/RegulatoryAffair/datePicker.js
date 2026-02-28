'use strict';
(function () {
    // Flat Picker
    // --------------------------------------------------------------------
    const flatpickrFriendly = document.querySelector('#dtPublish');
    // Human Friendly
    if (flatpickrFriendly) {
        flatpickrFriendly.flatpickr({
            altInput: true,
            altFormat: 'd.m.Y',
            dateFormat: 'Y-m-d',
            static: true
        });
    }

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
    window.txtDescription = new Quill('#txtDescription', {
        bounds: '#txtDescription',
        modules: {
            syntax: true,
            toolbar: '#txtDescriptionToolBar'
        },
        theme: 'snow'
    });

    window.snowEditor  = new Quill('#snow-editor', {
        bounds: '#snow-editor',
        modules: {
            syntax: true,
            toolbar: '#snow-toolbar'
        },
        theme: 'snow'
    });
    window.commentEditor = new Quill('#comment-editor', {
        bounds: '#comment-editor',
        modules: {
            syntax: true,
            toolbar: '#comment-toolbar'
        },
        theme: 'snow'
    });

})();
