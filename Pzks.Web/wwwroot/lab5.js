"use strict";

// Сторінка ЛР5: модель VLIW-системи.
const defaults = { processors: 2, banks: 1, add: 2, subtract: 3, multiply: 4, divide: 8, memory: 1 };
const paramIds = Object.keys(defaults);
const defaultExpression = "a+b+c*d+d/e-g*h+i/j";

let current = null;

const fmt = value => Number(value).toFixed(2);
const opLabel = op => op === "-" ? "−" : op;

// ---------- Параметри ----------

function readParams() {
    const params = {};
    for (const id of paramIds) {
        const value = parseInt($("p-" + id).value, 10);
        params[id] = Number.isNaN(value) ? defaults[id] : value;
    }
    return params;
}

function writeParams(params) {
    for (const id of paramIds)
        $("p-" + id).value = params[id];
}

// ---------- Тести ----------

async function loadSamples() {
    const samples = await (await fetch("/api/lab5/samples")).json();
    const box = el("div", { class: "sample-group" }, el("span", { class: "group-title" }, "Приклад ЛР5"));
    for (const sample of samples)
        box.append(sampleChip(sample, sample.group, () => build(sample.expression)));

    const lab2 = await (await fetch("/api/samples")).json();
    const other = el("div", { class: "sample-group" }, el("span", { class: "group-title" }, "Тести ЛР2"));
    for (const sample of lab2)
        other.append(sampleChip(sample, sample.group, () => build(sample.expression)));
    $("samples").replaceChildren(box, other);
}

// ---------- Побудова ----------

async function build(expression) {
    $("expression").value = expression;
    history.replaceState(null, "", "#" + encodeURIComponent(expression));
    $("status").className = "card status";
    $("status").textContent = "Моделювання…";
    show("result", true);
    current = await postJson("/api/lab5/simulate", { expression, ...readParams() });
    render(current);
}

function render(r) {
    const p = r.parallel;
    const lr1Valid = p.lr1Errors.length === 0;
    const ok = r.metrics != null;

    const status = $("status");
    status.className = "card status " + (ok ? "ok" : "bad");
    status.textContent = !lr1Valid
        ? `Вираз не пройшов перевірку ЛР1: помилок ${p.lr1Errors.length}. Модель не запускається.`
        : r.parameterErrors.length > 0 ? "Неправильні параметри системи."
        : p.errors.length > 0 ? "Перевірка ЛР1: помилок немає. Але вираз містить ділення на 0, тому дерево ЛР2 не будується."
        : r.graphErrors.length > 0 ? "Перевірка ЛР1: помилок немає. Але вираз містить функції, а VLIW виконує лише + − * /."
        : r.noOperations ? `Після спрощення ЛР2 вираз дорівнює ${p.parallel?.infix ?? p.simplified}: операцій немає, граф задачі не будується.`
        : `Перевірка ЛР1: помилок немає. Задач: ${r.tasks.length}, командних слів: ${r.metrics.words}, `
            + `T1 = ${r.metrics.sequential}, Tp = ${r.metrics.parallel}, Ky = ${fmt(r.metrics.speedup)}, E = ${fmt(r.metrics.efficiency)}.`;

    renderLr1(p, build);
    renderProblems(r);

    show("tree-card", lr1Valid && p.parallel != null && p.parallel.tiers > 0);
    if (p.parallel) {
        $("parallel-infix").textContent = p.parallel.infix;
        $("tree-svg").innerHTML = p.parallel.svg;
        $("tree-info").textContent = `· ярусів: ${p.parallel.tiers}, операцій: ${p.parallel.operations}, ширина: ${p.parallel.width}`;
    }

    for (const id of ["metrics", "graph-card", "gantt-card", "words-card", "calc-card", "sweep-card", "check-card"])
        show(id, ok);
    if (!ok) return;

    renderMetrics(r);
    renderGraph(r);
    renderGantt(r);
    renderWords(r);
    renderCalculation(r);
    renderSweep(r);
    renderCheck(r);
}

function renderProblems(r) {
    const p = r.parallel;
    let title = "", items = [];
    if (r.parameterErrors.length > 0) {
        title = "Неправильні параметри";
        items = r.parameterErrors;
    } else if (p.lr1Errors.length === 0 && p.errors.length > 0) {
        title = "Недопустимі операції: ділення на 0";
        items = p.errors.map(e => `терм ${e.term}: ${e.message}`);
    } else if (r.graphErrors.length > 0) {
        title = "Недопустимі операції для VLIW";
        items = r.graphErrors;
    }
    show("problem-card", items.length > 0);
    $("problem-title").textContent = title;
    $("problem-list").replaceChildren(...items.map(text => el("li", {}, text)));
}

function renderMetrics(r) {
    const m = r.metrics;
    const metric = (name, value, hint) => el("div", { class: "metric" },
        el("div", { class: "name" }, name), el("div", { class: "value" }, String(value)), el("div", { class: "hint" }, hint));
    $("metrics").replaceChildren(
        metric("T1 послідовно", m.sequential, "тактів на 1 процесорі"),
        metric("Tp паралельно", m.parallel, `тактів на ${m.processors} процесорах`),
        metric("Прискорення Ky", fmt(m.speedup), "Ky = T1 / Tp"),
        metric("Ефективність E", fmt(m.efficiency), `E = Ky / ${m.processors}` + (fmt(m.eucalyptusEfficiency) !== fmt(m.efficiency) ? ` · Eucalyptus: ${fmt(m.eucalyptusEfficiency)}` : "")),
        metric("E активних", fmt(m.activeEfficiency), `Ky / ${m.activeProcessors} (процесори з задачами)`),
        metric("Командних слів", m.words, `ширина дерева: ${m.treeWidth}`),
        metric("Конфлікти пам'яті", m.memoryWaits, "тактів очікування банку"));
}

function renderGraph(r) {
    $("graph-svg").innerHTML = r.graphSvg;
    $("graph-info").textContent = `· задач: ${r.tasks.length}, ярусів: ${r.metrics.tiers}`;
    $("tasks-body").replaceChildren(...r.tasks.map(t => el("tr", {},
        el("td", { class: "num" }, t.id),
        el("td", { class: "infix" }, t.text),
        el("td", { class: "infix" }, t.expression),
        el("td", { class: "num" }, t.tier),
        el("td", { class: "num" }, `${t.duration} (${opLabel(t.operator)})`),
        el("td", { class: "num" }, t.priority),
        el("td", { class: "num" }, t.word),
        el("td", { class: "num" }, `P${t.processor}`),
        el("td", { class: "num" }, `${t.start}–${t.end}`))));
}

function renderGantt(r) {
    $("gantt-svg").innerHTML = r.ganttSvg;
    const p = r.parameters;
    $("gantt-info").textContent = `· процесорів: ${p.processors}, банків пам'яті: ${p.banks}, Tp = ${r.metrics.parallel}`;
}

function renderWords(r) {
    const count = r.parameters.processors;
    const tasks = new Map(r.tasks.map(t => [t.id, t]));
    $("words-head").replaceChildren(el("tr", {},
        el("th", { class: "num" }, "Слово"), el("th", { class: "num" }, "Такти"), el("th", { class: "num" }, "Тривалість"),
        ...Array.from({ length: count }, (_, i) => el("th", {}, `P${i + 1}`))));
    $("words-body").replaceChildren(...r.words.map(w => el("tr", {},
        el("td", { class: "num" }, w.index),
        el("td", { class: "num" }, `${w.start}–${w.end}`),
        el("td", { class: "num" }, w.length),
        ...Array.from({ length: count }, (_, i) => {
            const c = w.commands.find(c => c.processor === i + 1);
            if (!c) return el("td", { class: "muted" }, "—");
            const t = tasks.get(c.task);
            const details = [`такти ${c.start}–${c.end}`, `читань ${c.reads}`];
            if (c.waits > 0) details.push(`очікування ${c.waits}`);
            return el("td", {}, el("span", { class: "op" }, el("b", {}, `[${t.id}]`), " ", t.text),
                el("div", { class: "muted small-text" }, details.join(", ")));
        }))));
}

function renderCalculation(r) {
    const m = r.metrics, p = r.parameters;
    const initial = r.tasks.filter(t => t.initial).length;
    const durations = r.tasks.reduce((sum, t) => sum + t.duration, 0);
    const line = (label, formula) => el("div", { class: "form-row" }, el("span", { class: "label" }, label), el("code", {}, formula));
    $("calc").replaceChildren(
        el("p", { class: "muted" }, "Час указано в тактах."),
        line("Послідовний час T1", `${initial}·${p.memory} (читання вхідних даних) + ${durations} (обчислення) + ${r.tasks.length}·${p.memory} (запис результатів) = ${m.sequential}`),
        line("Паралельний час Tp", `кінець останнього командного слова = ${m.parallel}`),
        line("Коефіцієнт прискорення", `Ky = T1 / Tp = ${m.sequential} / ${m.parallel} = ${fmt(m.speedup)}`),
        line("Ефективність (усі процесори)", `E = Ky / p = ${fmt(m.speedup)} / ${m.processors} = ${fmt(m.efficiency)}`),
        line("Ефективність (активні)", `Ea = Ky / pa = ${fmt(m.speedup)} / ${m.activeProcessors} = ${fmt(m.activeEfficiency)}`));

    const rows = r.processors.map(pr => el("tr", {},
        el("td", {}, `P${pr.id}`), el("td", { class: "num" }, pr.tasks), el("td", { class: "num" }, pr.reads),
        el("td", { class: "num" }, pr.compute), el("td", { class: "num" }, pr.writes), el("td", { class: "num" }, pr.waits),
        el("td", { class: "num" }, pr.useful), el("td", { class: "num" }, fmt(pr.efficiency))));
    for (const b of r.banks)
        rows.push(el("tr", {},
            el("td", {}, `Банк ${b.id}`), el("td", { class: "num muted" }, "—"), el("td", { class: "num" }, b.reads),
            el("td", { class: "num muted" }, "—"), el("td", { class: "num" }, b.writes), el("td", { class: "num muted" }, "—"),
            el("td", { class: "num" }, b.busy), el("td", { class: "num" }, fmt(b.busy / m.parallel))));
    $("load-body").replaceChildren(...rows);
}

function renderSweep(r) {
    $("sweep-info").textContent = `· банків пам'яті: ${r.parameters.banks}, T1 = ${r.metrics.sequential}`;
    $("sweep-body").replaceChildren(...r.sweep.map(s => el("tr", { class: s.processors === r.parameters.processors ? "best" : "" },
        el("td", { class: "num" }, s.processors), el("td", { class: "num" }, s.words), el("td", { class: "num" }, s.parallelTime),
        el("td", { class: "num" }, s.memoryWaits), el("td", { class: "num" }, fmt(s.speedup)), el("td", { class: "num" }, fmt(s.efficiency)))));
}

function renderCheck(r) {
    const p = r.parameters;
    $("check-system").textContent = `процесорів ${p.processors}, банків пам'яті ${p.banks}`;
    $("check-durations").textContent = `+ ${p.add}, − ${p.subtract}, * ${p.multiply}, / ${p.divide}`;
    $("check-memory").textContent = String(p.memory);
    const tasks = new Map(r.tasks.map(t => [t.id, t]));
    let step = 0;
    $("check-body").replaceChildren(...r.words.flatMap(w => w.commands.map(c => el("tr", {},
        el("td", { class: "num" }, ++step), el("td", { class: "num" }, w.index), el("td", { class: "num" }, c.task),
        el("td", { class: "infix" }, tasks.get(c.task).text), el("td", { class: "num" }, `P${c.processor}`),
        el("td", { class: "num" }, c.start)))));
}

// ---------- Збереження малюнків ----------

function saveImage(which, format) {
    const name = `lr5-${which}`;
    if (which === "tree") saveSvg(current?.parallel.parallel?.svg, name, format);
    else if (which === "graph") saveSvg(current?.graphSvg, name, format);
    else saveSvg(current?.ganttSvg, name, format);
}

// ---------- Старт ----------

$("form").addEventListener("submit", event => {
    event.preventDefault();
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

for (const id of paramIds)
    $("p-" + id).addEventListener("change", () => {
        const expression = $("expression").value.trim();
        if (expression) build(expression);
    });

$("reset").addEventListener("click", () => {
    writeParams(defaults);
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

document.querySelectorAll("[data-download]").forEach(button =>
    button.addEventListener("click", () => saveImage(button.dataset.download, button.dataset.format)));

loadSamples();
const fromHash = decodeURIComponent(location.hash.slice(1));
build(fromHash || defaultExpression);
