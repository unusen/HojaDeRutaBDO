(() => {
    const configElement = document.getElementById("createConfig");
    if (!configElement) {
        return;
    }

    const config = JSON.parse(configElement.textContent);
var subareas = config;

        //       (function () {
        //     let form = document.getElementById("hojaForm");
        //     let isDirty = false;
        //     debugger;

        //     // Detectar cambios en cualquier campo del formulario
        //     form.querySelectorAll("input, select, textarea").forEach(campo => {
        //         campo.addEventListener("change", () => {
        //             isDirty = true;
        //         });
        //         campo.addEventListener("input", () => {
        //             isDirty = true;
        //         });
        //     });

        //     // Desactivar advertencia al enviar el formulario
        //     form.addEventListener("submit", () => {
        //         isDirty = false;
        //     });

        //     // Detectar intento de cerrar o refrescar página
        //     window.addEventListener("beforeunload", function (e) {
        //         debugger;
        //         if (isDirty) {
        //             e.preventDefault();
        //             e.returnValue = "Hay cambios sin guardar. ¿Seguro que quieres salir?";
        //         }
        //     });
        // })();

        //     //VALIDACION PARA MODO CREACION O EDICION DEL BOTON
        //     document.getElementById("btnCrear").addEventListener("click", function (e) {
        //     e.preventDefault();

        //     const form = e.target.closest("form");
        //     const formData = new FormData(form);

        //     fetch(form.action, {
        //         method: "POST",
        //         body: formData
        //     })
        //     .then(response => response.json()) // tu acción debe devolver JSON con el Id
        //     .then(data => {
        //         if (data.id && data.id > 0) {
        //             document.getElementById("btnCrear").style.display = "none";
        //             document.getElementById("btnGuardar").style.display = "inline-block";
        //         }
        //     })
        //     .catch(err => console.error(err));
        // });


        function onSectorChange(select) {
                    debugger;
                var sector = select.value;
                cargarSubareas(sector);
        }


        // function cargarSubareas(sector, selectedSubareaId = null) {
        //     debugger;
        //     var subareaSelect = document.getElementById("Subarea");

        //     subareaSelect.innerHTML = '<option value="">Seleccione SubArea</option>';

        //     if (sectorId) {
        //         var filtradas = subareas.filter(s => s.Sector == sector);

        //         filtradas.forEach(function (s) {
        //             var option = document.createElement("option");
        //             option.value = s.Nombre;
        //             option.text = s.Detalle;

        //             // if (selectedSubareaId && selectedSubareaId == s.Id) {
        //             //     option.selected = true;
        //             // }

        //             subareaSelect.appendChild(option);
        //         });
        //     }
        // }

    
})();
