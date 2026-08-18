(() => {
    const configElement = document.getElementById("reportesConfig");
    if (!configElement) {
        return;
    }

    const config = JSON.parse(configElement.textContent);
document.addEventListener("DOMContentLoaded", function () {
            inicializarSeleccionColumnas();
            validateSocio();
            document.querySelectorAll("[data-date-toggle]").forEach(function (input) {
                input.addEventListener("focus", function () { this.type = "date"; });
                input.addEventListener("blur", function () {
                    if (this.value === "") {
                        this.type = "text";
                    }
                });
            });
        });

        function validateSocio()
        {
            debugger;

            const form = document.getElementById("formReporte");
            const socioSelect = document.getElementById("socio");
            const errorSpan = document.getElementById("validateSocio");

            function validarSocio(e) {
                let valido = true;

                if (!socioSelect.value.trim()) {
                    errorSpan.textContent = "Debe seleccionar un socio para generar el reporte";
                    socioSelect.classList.add("is-invalid");
                    valido = false;
                } else {
                    errorSpan.textContent = "";
                    socioSelect.classList.remove("is-invalid");
                    socioSelect.classList.add("is-valid");
                }

                if (!valido && e) {
                    e.preventDefault();
                }

                return valido;
            }

            form.addEventListener("submit", validarSocio);
            socioSelect.addEventListener("change", validarSocio);
        }


        async function inicializarSeleccionColumnas() {
            const contenedor = document.getElementById("columnasContainer");
            const res = await fetch(config.obtenerColumnasUrl);
            const columnas = await res.json();

            columnas.forEach(col => {
                const item = document.createElement("div");
                item.className = "columna-item d-flex align-items-center gap-2";
                item.innerHTML = `
                    <input class="me-1" type="checkbox" value="${col.column}" id="${col.propiedad}">
                    <label for="${col.propiedad}" class="flex-grow-1 mb-0">${col.nombre}</label>
                    <i class="bi bi-grip-vertical text-muted ms-auto"></i>
                `;
                contenedor.appendChild(item);
            });

            Sortable.create(contenedor, { animation: 150, handle: ".bi-grip-vertical" });

            inicializarEstiloSeleccionColumnas(contenedor);

            document.getElementById("formReporte").addEventListener("submit", function () {
                const seleccionadas = [];
                contenedor.querySelectorAll("input[type=checkbox]:checked")
                    .forEach(cb => seleccionadas.push(cb.value));
                document.getElementById("columnasSeleccionadas").value = seleccionadas.join(",");
            });
        }

        function inicializarEstiloSeleccionColumnas(contenedor) {
            const items = contenedor.querySelectorAll(".columna-item");

            items.forEach(item => {
                const checkbox = item.querySelector("input[type='checkbox']");

                if (checkbox.checked) item.classList.add("selected");

                item.addEventListener("click", (e) => {
                    if (e.target.tagName === "INPUT" || e.target.tagName === "I") return;

                    checkbox.checked = !checkbox.checked;
                    item.classList.toggle("selected", checkbox.checked);
                });

                checkbox.addEventListener("change", () => {
                    item.classList.toggle("selected", checkbox.checked);
                });
            });
        }
    
})();
