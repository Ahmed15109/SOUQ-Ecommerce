$(document).ready(function () {
    $(document).on('submit', 'form[data-confirm]', function (event) {
        if (!window.confirm($(this).data('confirm'))) {
            event.preventDefault();
        }
    });

    $(document).on('change', 'select[data-auto-submit]', function () {
        this.form.requestSubmit();
    });

    function formatCurrency(value) {
        return Number(value).toFixed(2) + ' ج.م';
    }

    function handleAjaxFailure(xhr) {
        if (xhr.status === 401) {
            window.location.href = xhr.getResponseHeader('Location') || '/Account/Login';
            return;
        }

        if (xhr.status === 403) {
            alert('غير مصرح بتنفيذ هذا الإجراء.');
            return;
        }

        alert('حدث خطأ في الاتصال');
    }
    // Setup Anti-Forgery token header for all AJAX POST requests
    var token = $('input[name="__RequestVerificationToken"]').val();
    if (token) {
        $.ajaxSetup({
            headers: {
                'RequestVerificationToken': token
            }
        });
    }

    // Favorites Toggle
    $(document).on('click', '.favorite-btn', function (e) {
        e.preventDefault();
        var btn = $(this);
        var productId = btn.data('id');

        if (!productId) {
            console.error("Product ID not found");
            return;
        }

        btn.prop('disabled', true);
        $.post('/Favorites/Toggle', { id: productId }, function (response) {
            if (response.success) {
                $('.bi-heart').next('.badge').text(response.count);

                if (response.isFavorite) {
                    btn.addClass('text-danger').removeClass('text-muted');
                    btn.find('i').removeClass('bi-heart').addClass('bi-heart-fill');
                } else {
                    btn.removeClass('text-danger').addClass('text-muted');
                    btn.find('i').removeClass('bi-heart-fill').addClass('bi-heart');

                    if (window.location.pathname.toLowerCase().includes('favorites')) {
                        btn.closest('.col').fadeOut();
                    }
                }
            } else {
                if (response.message) {
                    alert(response.message);
                }
            }
        }).fail(function (xhr) {
            handleAjaxFailure(xhr);
        }).always(function () {
            btn.prop('disabled', false);
        });
    });

    // Add To Cart
    $(document).on('click', '.add-to-cart-btn', function (e) {
        var btn = $(this);
        var productId = btn.data('id');

        if (productId) {
            var icon = btn.find('i');
            var isNewBtn = icon.hasClass('bi-cart-plus');
            var originalIconClass = isNewBtn ? 'bi-cart-plus' : 'bi-plus-lg';

            btn.prop('disabled', true);
            icon.removeClass(originalIconClass).addClass('bi-check-lg');

            $.post('/Cart/AddToCart', { id: productId }, function (response) {
                if (response.success) {
                    $('.bi-cart3').next('.badge').text(response.count);
                    setTimeout(function () {
                        icon.removeClass('bi-check-lg').addClass(originalIconClass);
                    }, 1000);
                } else {
                    icon.removeClass('bi-check-lg').addClass(originalIconClass);
                    if (response.message) {
                        alert(response.message);
                    }
                    if (response.redirect) {
                        window.location.href = response.redirect;
                    }
                }
            }).fail(function (xhr) {
                icon.removeClass('bi-check-lg').addClass(originalIconClass);
                handleAjaxFailure(xhr);
            }).always(function () {
                btn.prop('disabled', false);
            });
        }
    });

    // Update Quantity
    $(document).on('click', '.btn-update-qty', function () {
        var btn = $(this);
        var row = btn.closest('.cart-item');
        var productId = row.data('id');
        var change = parseInt(btn.data('change'));
        var input = row.find('.qty-input');
        var currentQty = parseInt(input.val());
        var newQty = currentQty + change;

        if (newQty < 1) return;

        btn.prop('disabled', true);
        $.post('/Cart/UpdateQuantity', { id: productId, qty: newQty }, function (response) {
            if (response.success) {
                input.val(newQty);
                row.find('.item-total').text(formatCurrency(response.itemTotal));
                $('#cart-subtotal').text(formatCurrency(response.cartTotal));
                $('#cart-total').text(formatCurrency(response.finalTotal));
                $('#cart-count').text(response.count);
                $('.bi-cart3').next('.badge').text(response.count);
            } else if (response.message) {
                alert(response.message);
            }
        }).fail(function (xhr) {
            handleAjaxFailure(xhr);
        }).always(function () {
            btn.prop('disabled', false);
        });
    });

    // Remove from Cart
    $(document).on('click', '.btn-remove', function () {
        var row = $(this).closest('.cart-item');
        var productId = row.data('id');

        row.find('.btn-remove').prop('disabled', true);
        $.post('/Cart/RemoveFromCart', { id: productId }, function (response) {
            if (response.success) {
                row.fadeOut(function () {
                    $(this).remove();
                    if ($('.cart-item').length === 0) location.reload();
                });
                $('#cart-subtotal').text(formatCurrency(response.cartTotal));
                $('#cart-total').text(formatCurrency(response.finalTotal));
                $('.bi-cart3').next('.badge').text(response.count);
                $('#cart-count').text(response.count);
            }
        }).fail(function (xhr) {
            row.find('.btn-remove').prop('disabled', false);
            handleAjaxFailure(xhr);
        });
    });
});
