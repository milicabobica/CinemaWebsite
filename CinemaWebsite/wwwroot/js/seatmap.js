document.addEventListener('DOMContentLoaded', function () {
    const seatMap = document.getElementById('seat-map');
    if (!seatMap) return;

    const pricePerSeat = parseFloat(seatMap.dataset.price || '0');
    const money = new Intl.NumberFormat(seatMap.dataset.currencyLocale, {
        style: 'currency',
        currency: seatMap.dataset.currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    const selected = new Set();

    function seatButton(id) {
        return seatMap.querySelector(`button.seat[data-seat-id="${id}"]`);
    }

    function updateSummary() {
        const selectedCountEl = document.getElementById('selectedCount');
        const totalPriceEl = document.getElementById('totalPrice');
        const seatPriceEl = document.getElementById('seatPrice');
        const ticketInput = document.querySelector('input[name="NumberOfTickets"]');

        const count = selected.size;
        if (selectedCountEl) selectedCountEl.textContent = String(count);
        if (seatPriceEl) seatPriceEl.textContent = money.format(pricePerSeat);
        if (totalPriceEl) totalPriceEl.textContent = money.format(pricePerSeat * count);
        if (ticketInput) ticketInput.value = String(count === 0 ? ticketInput.value : count);
    }

    function seatInput(id) {
        return seatMap.querySelector(`input.seat-select-input[data-seat-id="${id}"]`);
    }

    function selectSeat(id) {
        const btn = seatButton(id);
        const input = seatInput(id);
        if (!btn || btn.disabled) return;
        selected.add(id);
        btn.classList.add('selected');
        btn.setAttribute('aria-pressed', 'true');
        if (input && !input.disabled) input.checked = true;
    }

    function deselectSeat(id) {
        const btn = seatButton(id);
        const input = seatInput(id);
        selected.delete(id);
        if (btn) {
            btn.classList.remove('selected');
            btn.setAttribute('aria-pressed', 'false');
        }
        if (input) input.checked = false;
    }

    seatMap.addEventListener('click', function (e) {
        const btn = e.target.closest('button.seat.available');
        if (!btn) return;

        const id = btn.dataset.seatId;
        const pairId = btn.dataset.pairId;
        const isSelected = selected.has(id);

        if (!isSelected) {
            selectSeat(id);
            if (pairId) selectSeat(pairId);
        } else {
            deselectSeat(id);
            if (pairId) deselectSeat(pairId);
        }

        updateSummary();
    });

    // initialize summary in case seats are pre-selected
    updateSummary();
});
