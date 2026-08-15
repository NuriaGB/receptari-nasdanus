// rich-editor.js — helpers per a editors contenteditable

window.richEditor = {

    // Inicialitza un contenteditable: posa el contingut HTML i registra callback
    init: function (id, initialHtml, dotnetRef) {
        var el = document.getElementById(id);
        if (!el) return;
        if (initialHtml && el.innerHTML !== initialHtml)
            el.innerHTML = initialHtml;

        // Guardem referència per no duplicar listeners
        if (el._richEditorInit) return;
        el._richEditorInit = true;

        el.addEventListener('input', function () {
            dotnetRef.invokeMethodAsync('OnHtmlChanged', el.innerHTML);
        });

        el.addEventListener('keydown', function (e) {
            // Ctrl+B = negreta, Ctrl+I = cursiva (el navegador ja ho fa, però forcem execCommand)
            if ((e.ctrlKey || e.metaKey) && e.key === 'b') {
                e.preventDefault();
                document.execCommand('bold');
            }
            if ((e.ctrlKey || e.metaKey) && e.key === 'i') {
                e.preventDefault();
                document.execCommand('italic');
            }
        });
    },

    // Llegeix el text seleccionat (per linkar ingredients)
    getSelection: function (id) {
        var el = document.getElementById(id);
        if (!el) return null;
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return null;
        var text = sel.toString().trim();
        if (!text) return null;
        return { text: text };
    },

    // Posa el focus al final del contingut
    focus: function (id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.focus();
        var range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
        var sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
    }
};
