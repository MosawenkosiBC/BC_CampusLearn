window.createResourceRichTextEditor = (host, placeholder) => new window.Quill(host, {
    theme: "snow",
    placeholder,
    formats: ["header", "bold", "italic", "underline", "list", "blockquote", "code-block", "link"],
    modules: {
        toolbar: [
            [{ header: [2, 3, false] }],
            ["bold", "italic", "underline"],
            [{ list: "ordered" }, { list: "bullet" }],
            ["blockquote", "code-block"],
            ["link"],
            ["clean"]
        ]
    }
});
