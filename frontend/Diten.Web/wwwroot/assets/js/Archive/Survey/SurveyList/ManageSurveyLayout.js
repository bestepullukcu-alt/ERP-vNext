// Oluşturulacak form tipleri
const formElements = [
    {
        category: "Text Input",
        items: [
            { type: "short-text", title: "Short Text", desc: "Single line text input" },
            { type: "long-text", title: "Long Text", desc: "Multi-line text area" },
            { type: "number", title: "Number", desc: "Numeric input field" },
            { type: "numeric-range", title: "Numeric Range", desc: "Enter a range of numbers" }
        ]
    },
    {
        category: "Choice Questions",
        items: [
            { type: "single-choice", title: "Multiple Choice (Single)", desc: "Select one option" },
            { type: "multiple-choice", title: "Multiple Choice (Multiple)", desc: "Select multiple options" },
            { type: "dropdown", title: "Dropdown", desc: "Dropdown selection" }
        ]
    },
    {
        category: "Rating & Scales",
        items: [
            { type: "rating-scale", title: "Rating Scale", desc: "Numeric rating scale" },
            { type: "slider-scale", title: "Slider Scale", desc: "Interactive slider" }
        ]
    },
    {
        category: "Matrix",
        items: [
            { type: "matrix", title: "Matrix", desc: "" }
        ]
    },
    {
        category: "Contact Info",
        items: [
            { type: "email", title: "Email Address", desc: "" },
            { type: "phone", title: "Phone Number", desc: "" },
            { type: "address", title: "Address", desc: "" }
        ]
    },
    {
        category: "Date & Time",
        items: [
            { type: "date", title: "Date", desc: "" },
            { type: "time", title: "Time", desc: "" }
        ]
    },
    {
        category: "Media",
        items: [
            { type: "image-upload", title: "Image Upload", desc: "" },
            { type: "file-upload", title: "File Upload", desc: "" },
            { type: "video", title: "Video", desc: "" },
            { type: "website-url", title: "Website Url", desc: "" },
        ]
    }
];

// Listeyi render et
const container = document.getElementById("formElementList");

formElements.forEach(group => {
    // Kategori başlığı
    const headerLi = document.createElement("li");
    headerLi.classList.add("menu-header", "small");
    headerLi.innerHTML = `
        <span class="menu-header-text">${group.category}</span>
      `;
    container.appendChild(headerLi);

    // Grup içi liste
    

    group.items.forEach(item => {
        const listGroup = document.createElement("div");
        listGroup.className = "list-group mb-1 px-1";
        const btn = document.createElement("button");
        btn.className = "list-group-item list-group-item-action";
        btn.dataset.type = item.type;
        btn.innerHTML = `${item.title}${item.desc ? `<br><small class="text-muted">${item.desc}</small>` : ""}`;
        if (["image-upload", "file-upload", "video"].includes(item.type)) {
            btn.disabled = true;
            btn.classList.add("disabled"); // bootstrap görünümü için
        }

        listGroup.appendChild(btn);
        container.appendChild(listGroup);
    });

   
});


