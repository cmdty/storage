import pandas as pd
import ipywidgets as ipw
import ipysheet as ips
from cmdty_storage import CmdtyStorage, three_factor_seasonal_value, MultiFactorModel, multi_factor, RatchetInterp
from curves import max_smooth_interp, adjustments
from datetime import date, timedelta
from IPython.display import display
from ipywidgets.widgets.interaction import show_inline_matplotlib_plots
from collections import namedtuple
import itertools
import logging
import csv
import os
from PyQt5.QtWidgets import QFileDialog, QApplication
from datetime import datetime
import os


# Workaround to stop Python Kernel crashing due to multiple OpenMP dlls being loaded
os.environ['KMP_DUPLICATE_LIB_OK']='True'

# Shared variables
freq = 'D'
num_fwd_rows = 28
date_format = 'YYYY-MM-DD'
num_ratch_rows = 20
RatchetRow = namedtuple('RatchetRow', ['date', 'inventory', 'inject_rate', 'withdraw_rate'])


def str_to_bool(bool_text: str) -> bool:
    bool_text_lower = bool_text.lower()
    if bool_text_lower == 'true':
        return True
    elif bool_text_lower == 'false':
        return False
    else:
        raise ValueError('bool_text parameter value of \'{}\' cannot be parsed to boolean.'.format(bool_text))


def select_file_open(header, filter):
    dir = './'
    app = QApplication([dir])
    file_name = QFileDialog.getOpenFileName(None, header, dir, filter=filter)
    return file_name[0]


def select_file_save(header, filter, default_file_name):
    dir = './'
    app = QApplication([dir])
    default_file_path = os.path.join(dir, default_file_name)
    file_name = QFileDialog.getSaveFileName(None, header, default_file_path, filter=filter)
    return file_name[0]


def save_dict_to_csv(file_path, data_dict):
    with open(file_path, mode='w', newline='') as csv_file:
        csv_writer = csv.writer(csv_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
        csv_writer.writerow(['key', 'value'])
        for key, value in data_dict.items():
            csv_writer.writerow([key, value])


def load_csv_to_dict(file_path) -> dict:
    data_dict = {}
    with open(file_path, mode='r') as csv_file:
        csv_reader = csv.reader(csv_file, delimiter=',')
        line_count = 0
        for row in csv_reader:
            if line_count == 0:
                header_text = ','.join(row)
                if header_text != 'key,value':
                    raise ValueError('Storage details header row must be \'key,value\' but is \'' + header_text + '\'.')
            else:
                data_dict[row[0]] = row[1]
            line_count += 1
    return data_dict


def dataframe_to_ipysheet(dataframe):
    columns = dataframe.columns.tolist()
    rows = dataframe.index.tolist()
    cells = []
    cells.append(ips.Cell(
        value=[p.strftime('%Y-%m-%d') for p in dataframe.index],
        row_start=0,
        row_end=len(rows) - 1,
        column_start=0,
        column_end=0,
        type='date',
        date_format='YYYY-MM-DD',
        squeeze_row=False,
        squeeze_column=True
    ))
    idx = 1
    for c in columns:
        cells.append(ips.Cell(
            value=dataframe[c].values,
            row_start=0,
            row_end=len(rows) - 1,
            column_start=idx,
            column_end=idx,
            type='numeric',
            numeric_format='0.00',
            squeeze_row=False,
            squeeze_column=True
        ))
        idx += 1

    return ips.Sheet(
        rows=len(rows),
        columns=len(columns) + 1,
        cells=cells,
        row_headers=False,
        column_headers=['period'] + [str(header) for header in columns])


# Set up logging
class OutputWidgetHandler(logging.Handler):
    """ Custom logging handler sending logs to an output widget """

    def __init__(self, *args, **kwargs):
        super(OutputWidgetHandler, self).__init__(*args, **kwargs)
        layout = {
            'width': '50%',
            'height': '160px',
            'border': '1px solid black',
            'overflow_y': 'auto',
        }
        self.out = ipw.Output(layout=layout)

    def emit(self, record):
        """ Overload of logging.Handler method """
        formatted_record = self.format(record)
        new_output = {
            'name': 'stdout',
            'output_type': 'stream',
            'text': formatted_record + '\n'
        }
        self.out.outputs = (new_output,) + self.out.outputs

    def clear_logs(self):
        """ Clear the current logs """
        self.out.clear_output()


logger = logging.getLogger('storage_gui')
log_handler = OutputWidgetHandler()
log_handler.setFormatter(logging.Formatter('%(asctime)s  - [%(levelname)s] %(message)s'))
logger.addHandler(log_handler)
logger.setLevel(logging.INFO)

log_level_wgt = ipw.Dropdown(description='Log Level',
                             options=['Debug', 'Info', 'Warning', 'Error', 'Critical'],
                             value='Info')

multi_factor.logger.addHandler(log_handler)
multi_factor.logger.setLevel(logging.INFO)


def on_log_level_change(change):
    try:
        level_text = change['new']
        level_int = getattr(logging, level_text.upper())
        logger.setLevel(level_int)
        multi_factor.logger.setLevel(level_int)
    except Exception as e:
        logger.exception(e)


log_level_wgt.observe(on_log_level_change, names='value')


def on_clear_logs_clicked(b):
    try:
        log_handler.clear_logs()
    except Exception as e:
        logger.exception(e)

btn_clear_logs = ipw.Button(description='Clear Log Display')
btn_clear_logs.on_click(on_clear_logs_clicked)


def create_tab(titles, children):
    tab = ipw.Tab()
    for idx, title in enumerate(titles):
        tab.set_title(idx, title)
    tab.children = children
    return tab


def enumerate_ratchets():
    ratchet_row = 0
    while ratchet_row < num_ratch_rows and ratchet_input_sheet.cells[1].value[ratchet_row] != '':
        yield RatchetRow(ratchet_input_sheet.cells[0].value[ratchet_row], ratchet_input_sheet.cells[1].value[ratchet_row],
                         ratchet_input_sheet.cells[2].value[ratchet_row], ratchet_input_sheet.cells[3].value[ratchet_row])
        ratchet_row += 1


def read_ratchets():
    ratchets = []
    for ratchet in enumerate_ratchets():
        if ratchet.date != '':
            dt_item = (pd.Period(ratchet.date, freq=freq), [(ratchet.inventory, -ratchet.inject_rate,
                                                             ratchet.withdraw_rate)])
            ratchets.append(dt_item)
        else:
            dt_item[1].append((ratchet.inventory, -ratchet.inject_rate,
                               ratchet.withdraw_rate))
    return ratchets


# ======================================================================================================
# VALUATION DATA


def on_load_val_data_clicked(b):
    try:
        val_data_path = select_file_open('Select valuation data file', 'CSV File (*.csv)')
        if val_data_path != '':
            val_data_dict = load_csv_to_dict(val_data_path)
            val_date_wgt.value = datetime.strptime(val_data_dict['val_date'], '%Y-%m-%d').date()
            inventory_wgt.value = val_data_dict['inventory']
            ir_wgt.value = val_data_dict['interest_rate']
            discount_deltas_wgt.value = str_to_bool(val_data_dict['discount_deltas'])
    except Exception as e:
        logger.exception(e)


def on_save_val_data_clicked(b):
    try:
        val_data_path = select_file_save('Save valuation data to', 'CSV File (*.csv)', 'val_data.csv')
        if val_data_path != '':
            val_data_dict = val_data_to_dict()
            save_dict_to_csv(val_data_path, val_data_dict)
    except Exception as e:
        logger.exception(e)


btn_load_val_data_wgt = ipw.Button(description='Load Valuation Data')
btn_load_val_data_wgt.on_click(on_load_val_data_clicked)
btn_save_val_data_wgt = ipw.Button(description='Save Valuation Data')
btn_save_val_data_wgt.on_click(on_save_val_data_clicked)
val_data_buttons = ipw.HBox([btn_load_val_data_wgt, btn_save_val_data_wgt])

val_date_wgt = ipw.DatePicker(description='Val Date', value=date.today())
inventory_wgt = ipw.FloatText(description='Inventory')
ir_wgt = ipw.FloatText(description='Intrst Rate %', step=0.005)
discount_deltas_wgt = ipw.Checkbox(description='Discount Deltas', value=False)
val_inputs_wgt = ipw.VBox([val_data_buttons, val_date_wgt, inventory_wgt, ir_wgt, discount_deltas_wgt])


def val_data_to_dict() -> dict:
    val_data_dict = {'val_date': val_date_wgt.value,
                     'inventory': inventory_wgt.value,
                     'interest_rate': ir_wgt.value,
                     'discount_deltas': discount_deltas_wgt.value}
    return val_data_dict


# ======================================================================================================
# FORWARD CURVE

def create_fwd_input_sheet(dates, prices, num_rows):
    if len(dates) > num_rows:
        raise ValueError('Length of dates cannot exceed number of rows {}.'.format(num_rows))
    if len(prices) > num_rows:
        raise ValueError('Length of prices cannot exceed number of rows {}.'.format(num_rows))
    dates = dates + [None] * (num_rows - len(dates))
    prices = prices + [None] * (num_rows - len(prices))
    dates_cells = ips.Cell(value=dates, row_start=0, row_end=len(dates) - 1, column_start=0,
                           column_end=0, type='date', date_format=date_format, squeeze_row=False, squeeze_column=True)
    prices_cells = ips.Cell(value=prices, row_start=0, row_end=len(prices) - 1, column_start=1,
                            column_end=1, type='numeric', numeric_format='0.000', squeeze_row=False,
                            squeeze_column=True)
    cells = [dates_cells, prices_cells]
    return ips.Sheet(rows=len(dates), columns=2, cells=cells, row_headers=False,
                     column_headers=['fwd_start', 'price'])


def reset_fwd_input_sheet(new_fwd_input_sheet):
    # This code is very bad and brittle, but necessary hack to be able to update the fwd input sheet quickly
    tuple_with_fwd_input = fwd_data_wgt.children[0].children
    fwd_data_wgt.children[0].children = tuple_with_fwd_input[0:5] + (new_fwd_input_sheet,)
    global fwd_input_sheet
    fwd_input_sheet = new_fwd_input_sheet


def on_load_curve_params(b):
    try:
        curve_params_path = select_file_open('Select curve parameters file', 'CSV File (*.csv)')
        if curve_params_path != '':
            curve_params_dict = load_csv_to_dict(curve_params_path)
            smooth_curve_wgt.value = str_to_bool(curve_params_dict['smooth_curve'])
            apply_wkend_shaping_wgt.value = str_to_bool(curve_params_dict['apply_weekend_shaping'])
            wkend_factor_wgt.value = curve_params_dict['weekend_shaping_factor']
    except Exception as e:
        logger.exception(e)

def on_save_curve_params(b):
    try:
        curve_params_path = select_file_save('Save curve params to', 'CSV File (*.csv)', 'curve_params.csv')
        if curve_params_path != '':
            curve_params_dict = {'smooth_curve': smooth_curve_wgt.value,
                                 'apply_weekend_shaping': apply_wkend_shaping_wgt.value,
                                 'weekend_shaping_factor': wkend_factor_wgt.value}
            save_dict_to_csv(curve_params_path, curve_params_dict)
    except Exception as e:
        logger.exception(e)


btn_load_curve_params_wgt = ipw.Button(description='Load Curve Params')
btn_load_curve_params_wgt.on_click(on_load_curve_params)
btn_save_curve_params_wgt = ipw.Button(description='Save Curve Params')
btn_save_curve_params_wgt.on_click(on_save_curve_params)
curve_params_buttons = ipw.HBox([btn_load_curve_params_wgt, btn_save_curve_params_wgt])

fwd_input_sheet = create_fwd_input_sheet([''] * num_fwd_rows, [''] * num_fwd_rows, num_fwd_rows)

out_fwd_curve = ipw.Output()
smooth_curve_wgt = ipw.Checkbox(description='Apply Smoothing', value=False)
apply_wkend_shaping_wgt = ipw.Checkbox(description='Wkend Shaping', value=False, disabled=True)
wkend_factor_wgt = ipw.FloatText(description='Wkend shaping factor', step=0.005, disabled=True)
btn_plot_fwd_wgt = ipw.Button(description='Plot Forward Curve')
btn_export_daily_fwd_wgt = ipw.Button(description='Export Daily Curve')
btn_import_fwd_wgt = ipw.Button(description='Import Forward Curve')
btn_export_fwd_wgt = ipw.Button(description='Export Forward Curve')
btn_clear_fwd_wgt = ipw.Button(description='Clear Forward Curve')


def on_smooth_curve_change(change):
    apply_wkend_shaping_wgt.disabled = not change['new']


smooth_curve_wgt.observe(on_smooth_curve_change, names='value')


def on_apply_wkend_shaping_change(change):
    wkend_factor_wgt.disabled = not change['new']


apply_wkend_shaping_wgt.observe(on_apply_wkend_shaping_change, names='value')


def on_plot_fwd_clicked(b):
    try:
        out_fwd_curve.clear_output()
        curve = read_fwd_curve()
        with out_fwd_curve:
            curve.plot()
            show_inline_matplotlib_plots()
    except Exception as e:
        logger.exception(e)


def on_export_daily_fwd_clicked(b):
    try:
        fwd_curve_path = select_file_save('Save daily forward curve to', 'CSV File (*.csv)', 'daily_fwd_curve.csv')
        if fwd_curve_path != '':
            curve = read_fwd_curve()
            curve.to_csv(fwd_curve_path, index_label='date', header=['price'])
    except Exception as e:
        logger.exception(e)


btn_plot_fwd_wgt.on_click(on_plot_fwd_clicked)
btn_export_daily_fwd_wgt.on_click(on_export_daily_fwd_clicked)


def on_import_fwd_curve_clicked(b):
    try:
        fwd_curve_path = select_file_open('Select forward curve file', 'CSV File (*.csv)')
        if fwd_curve_path != '':
            fwd_dates = []
            fwd_prices = []
            with open(fwd_curve_path, mode='r') as fwd_csv_file:
                csv_reader = csv.DictReader(fwd_csv_file)
                line_count = 0
                for row in csv_reader:
                    if line_count == 0:
                        header_text = ','.join(row)
                        if header_text != 'contract_start,price':
                            raise ValueError('Forward curve header row must be \'contract_start,price\'.')
                    fwd_dates.append(row['contract_start'])
                    fwd_prices.append(float(row['price']))
                    line_count += 1
            imported_fwd_input_sheet = create_fwd_input_sheet(fwd_dates, fwd_prices, num_fwd_rows)
            reset_fwd_input_sheet(imported_fwd_input_sheet)
    except Exception as e:
        logger.exception(e)


def on_export_fwd_curve_clicked(b):
    try:
        fwd_curve_path = select_file_save('Save forward curve to', 'CSV File (*.csv)', 'fwd_curve_data.csv')
        if fwd_curve_path != '':
            rows = []
            fwd_row = 0
            for fwd_start, fwd_price in enumerate_fwd_points():
                row = {'contract_start': fwd_start,
                       'price': fwd_price}
                rows.append(row)
                fwd_row += 1
            with open(fwd_curve_path, mode='w', newline='') as fwd_csv_file:
                writer = csv.DictWriter(fwd_csv_file, fieldnames=['contract_start', 'price'])
                writer.writeheader()
                writer.writerows(rows)
    except Exception as e:
        logger.exception(e)


def on_clear_fwd_curve_clicked(b):
    try:
        new_fwd_input_sheet = create_fwd_input_sheet([], [], num_fwd_rows)
        reset_fwd_input_sheet(new_fwd_input_sheet)
    except Exception as e:
        logger.exception(e)


btn_import_fwd_wgt.on_click(on_import_fwd_curve_clicked)
btn_export_fwd_wgt.on_click(on_export_fwd_curve_clicked)
btn_clear_fwd_wgt.on_click(on_clear_fwd_curve_clicked)

fwd_data_wgt = ipw.HBox([ipw.VBox([curve_params_buttons, smooth_curve_wgt, apply_wkend_shaping_wgt, wkend_factor_wgt,
                                   ipw.HBox([ipw.VBox([btn_import_fwd_wgt, btn_clear_fwd_wgt]), btn_export_fwd_wgt]),
                                   fwd_input_sheet]),
                         ipw.VBox([btn_plot_fwd_wgt, btn_export_daily_fwd_wgt, out_fwd_curve])])


# ======================================================================================================
# STORAGE DETAILS


def create_numeric_col(values, col_num):
    return ips.Cell(value=values, row_start=0, row_end=len(values) - 1, column_start=col_num,
                    column_end=col_num, type='numeric', numeric_format='0.000', squeeze_row=False,
                    squeeze_column=True)


def create_ratchets_sheet(dates, inventories, inject_rates, withdraw_rates, num_rows):
    if len(inventories) > num_rows:
        raise ValueError('Length of inventories in ratchets cannot exceed number of rows {}.'.format(num_rows))
    dates = dates + [''] * (num_rows - len(dates))
    inventories = inventories + [''] * (num_rows - len(inventories))
    inject_rates = inject_rates + [''] * (num_rows - len(inject_rates))
    withdraw_rates = withdraw_rates + [''] * (num_rows - len(withdraw_rates))
    dates_cells = ips.Cell(value=dates, row_start=0, row_end=len(dates) - 1, column_start=0,
                           column_end=0, type='date', date_format=date_format, squeeze_row=False, squeeze_column=True)
    inventory_cells = create_numeric_col(inventories, 1)
    inject_rate_cells = create_numeric_col(inject_rates, 2)
    withdraw_rate_cells = create_numeric_col(withdraw_rates, 3)
    cells = [dates_cells, inventory_cells, inject_rate_cells, withdraw_rate_cells]
    return ips.Sheet(rows=len(dates), columns=4, cells=cells, row_headers=False,
                     column_headers=['date', 'inventory', 'inject_rate', 'withdraw_rate'])


def on_save_storage_details_clicked(b):
    try:
        save_path = select_file_save('Save storage details to', 'CSV File (*.csv)', 'storage_details.csv')
        if save_path != '':
            with open(save_path, mode='w', newline='') as storage_details_file:
                details_writer = csv.writer(storage_details_file, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
                details_writer.writerow(['key', 'value'])
                details_writer.writerow(['storage_start', start_wgt.value])
                details_writer.writerow(['storage_end', end_wgt.value])
                details_writer.writerow(['injection_cost', inj_cost_wgt.value])
                details_writer.writerow(['withdrawal_cost', with_cost_wgt.value])
                details_writer.writerow(['cmdty_consumed_inject', inj_consumed_wgt.value])
                details_writer.writerow(['cmdty_consumed_withdraw', with_consumed_wgt.value])
                storage_type = stor_type_wgt.value.lower()
                details_writer.writerow(['storage_type', storage_type])
                if storage_type == 'simple':
                    details_writer.writerow(['min_inventory', invent_min_wgt.value])
                    details_writer.writerow(['max_inventory', invent_max_wgt.value])
                    details_writer.writerow(['max_injection_rate', inj_rate_wgt.value])
                    details_writer.writerow(['max_withdrawal_rate', with_rate_wgt.value])
            if storage_type == 'ratchets':
                ratchets_save_path = select_file_save('Save storage ratchets to', 'CSV File (*.csv)',
                                                      'storage_ratchets.csv')
                if ratchets_save_path != '':
                    with open(ratchets_save_path, mode='w', newline='') as storage_ratchets_file:
                        ratchets_writer = csv.writer(storage_ratchets_file, delimiter=',', quotechar='"',
                                                     quoting=csv.QUOTE_MINIMAL)
                        ratchets_writer.writerow(['date', 'inventory', 'inject_rate', 'withdraw_rate'])
                        for ratchet in enumerate_ratchets():
                            ratchets_writer.writerow(
                                [ratchet.date, ratchet.inventory, ratchet.inject_rate, ratchet.withdraw_rate])
    except Exception as e:
        logger.exception(e)


def on_load_storage_details_clicked(b):
    try:
        load_path = select_file_open('Open storage details from', 'CSV File (*.csv)')
        if load_path != '':
            details_dict = load_csv_to_dict(load_path)
            start_wgt.value = datetime.strptime(details_dict['storage_start'], '%Y-%m-%d').date()
            end_wgt.value = datetime.strptime(details_dict['storage_end'], '%Y-%m-%d').date()
            inj_cost_wgt.value = details_dict['injection_cost']
            with_cost_wgt.value = details_dict['withdrawal_cost']
            inj_consumed_wgt.value = details_dict['cmdty_consumed_inject']
            with_consumed_wgt.value = details_dict['cmdty_consumed_withdraw']
            storage_type = details_dict['storage_type']
            if storage_type == 'simple':
                stor_type_wgt.value = 'Simple'
                invent_min_wgt.value = details_dict['min_inventory']
                invent_max_wgt.value = details_dict['max_inventory']
                inj_rate_wgt.value = details_dict['max_injection_rate']
                with_rate_wgt.value = details_dict['max_withdrawal_rate']
            if storage_type == 'ratchets':
                ratchets_load_path = select_file_open('Open storage details from', 'CSV File (*.csv)')
                if ratchets_load_path != '':
                    dates = []
                    inventories = []
                    inject_rates = []
                    withdraw_rates = []
                    with open(ratchets_load_path, mode='r') as ratchets_file:
                        csv_reader = csv.reader(ratchets_file, delimiter=',')
                        line_count = 0
                        for row in csv_reader:
                            if line_count == 0:
                                header_text = ','.join(row)
                                if header_text != 'date,inventory,inject_rate,withdraw_rate':
                                    raise ValueError(
                                        'Storage details header row must be \'date,inventory,inject_rate,withdraw_rate\' but is \'' + header_text + '\'.')
                            else:
                                dates.append(row[0])
                                inventories.append(float(row[1]))
                                inject_rates.append(float(row[2]))
                                withdraw_rates.append(float(row[3]))
                            line_count += 1
                    new_ratchets_sheet = create_ratchets_sheet(dates, inventories, inject_rates, withdraw_rates,
                                                               num_ratch_rows)
                    reset_ratchets_sheet(new_ratchets_sheet)
                    stor_type_wgt.value = 'Ratchets'
    except Exception as e:
        logger.exception(e)


def reset_ratchets_sheet(new_ratchets_sheet):
    try:
        global ratchet_input_sheet
        ratchet_input_sheet = new_ratchets_sheet
        storage_details_wgt.children = (storage_common_wgt, ipw.VBox([btn_clear_ratchets_wgt, ratchet_input_sheet]))
    except Exception as e:
        logger.exception(e)


def on_clear_ratchets_clicked(b):
    try:
        new_ratchets_sheet = create_ratchets_sheet([], [], [], [], num_ratch_rows)
        reset_ratchets_sheet(new_ratchets_sheet)
    except Exception as e:
        logger.exception(e)


btn_save_storage_details_wgt = ipw.Button(description='Save Storage Details')
btn_save_storage_details_wgt.on_click(on_save_storage_details_clicked)
btn_load_storage_details_wgt = ipw.Button(description='Load Storage Details')
btn_load_storage_details_wgt.on_click(on_load_storage_details_clicked)
btn_clear_ratchets_wgt = ipw.Button(description='Clear Ratchets')
btn_clear_ratchets_wgt.on_click(on_clear_ratchets_clicked)
storage_load_save_hbox = ipw.HBox([btn_load_storage_details_wgt, btn_save_storage_details_wgt])

# Common storage properties
stor_type_wgt = ipw.RadioButtons(options=['Simple', 'Ratchets'], description='Storage Type')
start_wgt = ipw.DatePicker(description='Start')
end_wgt = ipw.DatePicker(description='End')
inj_cost_wgt = ipw.FloatText(description='Injection Cost')
inj_consumed_wgt = ipw.FloatText(description='Inj % Consumed', step=0.001)
with_cost_wgt = ipw.FloatText(description='Withdrw Cost')
with_consumed_wgt = ipw.FloatText(description='With % Consumed', step=0.001)

storage_common_wgt = ipw.VBox([storage_load_save_hbox,
                           ipw.HBox([ipw.VBox([
                           start_wgt, end_wgt, inj_cost_wgt, with_cost_wgt]),
                           ipw.VBox([stor_type_wgt, inj_consumed_wgt, with_consumed_wgt])])])

# Simple storage type properties
invent_min_wgt = ipw.FloatText(description='Min Inventory')
invent_max_wgt = ipw.FloatText(description='Max Inventory')
inj_rate_wgt = ipw.FloatText(description='Injection Rate')
with_rate_wgt = ipw.FloatText(description='Withdrw Rate')
storage_simple_wgt = ipw.VBox([invent_min_wgt, invent_max_wgt, inj_rate_wgt, with_rate_wgt])

# Ratchet storage type properties
ratchet_input_sheet = create_ratchets_sheet([], [], [], [], num_ratch_rows)

# Compose storage
storage_details_wgt = ipw.VBox([storage_common_wgt, storage_simple_wgt])


def on_stor_type_change(change):
    if change['new'] == 'Simple':
        storage_details_wgt.children = (storage_common_wgt, storage_simple_wgt)
    else:
        storage_details_wgt.children = (storage_common_wgt, ipw.VBox([btn_clear_ratchets_wgt, ratchet_input_sheet]))


stor_type_wgt.observe(on_stor_type_change, names='value')


# ======================================================================================================
# VOLATILITY PARAMS


def on_load_vol_params_clicked(b):
    try:
        vol_data_path = select_file_open('Select volatility parameters file', 'CSV File (*.csv)')
        if vol_data_path != '':
            vol_data_dict = load_csv_to_dict(vol_data_path)
            spot_mr_wgt.value = vol_data_dict['spot_mean_reversion']
            spot_vol_wgt.value = vol_data_dict['spot_vol']
            lt_vol_wgt.value = vol_data_dict['long_term_vol']
            seas_vol_wgt.value = vol_data_dict['seasonal_vol']
    except Exception as e:
        logger.exception(e)


def on_save_vol_params_clicked(b):
    try:
        vol_params_path = select_file_save('Save volatility parameters to', 'CSV File (*.csv)', 'vol_params.csv')
        if vol_params_path != '':
            vol_params_dict = vol_data_to_dict()
            save_dict_to_csv(vol_params_path, vol_params_dict)
    except Exception as e:
        logger.exception(e)


btn_load_vol_params_wgt = ipw.Button(description='Load Vol Params')
btn_load_vol_params_wgt.on_click(on_load_vol_params_clicked)
btn_save_vol_params_wgt = ipw.Button(description='Save Vol Params')
btn_save_vol_params_wgt.on_click(on_save_vol_params_clicked)
vol_params_buttons = ipw.HBox([btn_load_vol_params_wgt, btn_save_vol_params_wgt])

spot_vol_wgt = ipw.FloatText(description='Spot Vol', step=0.01)
spot_mr_wgt = ipw.FloatText(description='Spot Mean Rev', step=0.01)
lt_vol_wgt = ipw.FloatText(description='Long Term Vol', step=0.01)
seas_vol_wgt = ipw.FloatText(description='Seasonal Vol', step=0.01)
btn_plot_vol = ipw.Button(description='Plot Forward Vol')
out_vols = ipw.Output()
vol_params_wgt = ipw.HBox([ipw.VBox([vol_params_buttons, spot_vol_wgt, spot_mr_wgt, lt_vol_wgt, seas_vol_wgt,
                                     btn_plot_vol]), out_vols])


def vol_data_to_dict() -> dict:
    return {'spot_mean_reversion': spot_mr_wgt.value,
            'spot_vol': spot_vol_wgt.value,
            'long_term_vol': lt_vol_wgt.value,
            'seasonal_vol': seas_vol_wgt.value}


# Plotting vol
def btn_plot_vol_clicked(b):
    out_vols.clear_output()
    with out_vols:
        if val_date_wgt.value is None or end_wgt.value is None:
            print('Enter val date and storage end date.')
            return
        vol_model = MultiFactorModel.for_3_factor_seasonal(freq, spot_mr_wgt.value, spot_vol_wgt.value,
                                                           lt_vol_wgt.value, seas_vol_wgt.value, val_date_wgt.value,
                                                           end_wgt.value)
        start_vol_period = pd.Period(val_date_wgt.value, freq=freq)
        end_vol_period = start_vol_period + 1
        periods = pd.period_range(start=end_vol_period, end=end_wgt.value, freq=freq)
        fwd_vols = [vol_model.integrated_vol(start_vol_period, end_vol_period, period) for period in periods]
        fwd_vol_series = pd.Series(data=fwd_vols, index=periods)
        fwd_vol_series.plot()
        show_inline_matplotlib_plots()


btn_plot_vol.on_click(btn_plot_vol_clicked)


# ======================================================================================================
# TECHNICAL PARAMETERS


def on_load_tech_params(b):
    try:
        tech_params_path = select_file_open('Select technical params file', 'CSV File (*.csv)')
        if tech_params_path != '':
            tech_params_dict = load_csv_to_dict(tech_params_path)
            num_sims_wgt.value = tech_params_dict['num_sims']
            basis_funcs_input_wgt.value = tech_params_dict['basis_funcs']
            random_seed_wgt.value = tech_params_dict['seed']
            seed_is_random_wgt.value = str_to_bool(tech_params_dict['seed_is_random'])
            fwd_sim_seed_wgt.value = tech_params_dict['fwd_sim_seed']
            fwd_sim_seed_set_wgt.value = str_to_bool(tech_params_dict['set_fwd_sim_seed'])
            extra_decisions_wgt.value = tech_params_dict['extra_decisions']
            grid_points_wgt.value = tech_params_dict['num_inventory_grid_points']
            num_tol_wgt.value = tech_params_dict['numerical_tolerance']
    except Exception as e:
        logger.exception(e)


def on_save_tech_params(b):
    try:
        tech_params_path = select_file_save('Save technical params to', 'CSV File (*.csv)', 'tech_params.csv')
        if tech_params_path != '':
            tech_params_dict = tech_params_to_dict()
            save_dict_to_csv(tech_params_path, tech_params_dict)
    except Exception as e:
        logger.exception(e)


btn_load_tech_params = ipw.Button(description='Load Tech Params')
btn_load_tech_params.on_click(on_load_tech_params)
btn_save_tech_params = ipw.Button(description='Save Tech Params')
btn_save_tech_params.on_click(on_save_tech_params)
tech_params_buttons = ipw.HBox([btn_load_tech_params, btn_save_tech_params])

num_sims_wgt = ipw.IntText(description='Num Sims', value=4000, step=500)
extra_decisions_wgt = ipw.IntText(description='Extra Decisions', value=0, step=1)
seed_is_random_wgt = ipw.Checkbox(description='Seed is Random', value=False)
random_seed_wgt = ipw.IntText(description='Seed', value=11)
fwd_sim_seed_set_wgt = ipw.Checkbox(description='Set fwd sim seed', value=True)
fwd_sim_seed_wgt = ipw.IntText(description='Fwd Sim Seed', value=13, disabled=False)
grid_points_wgt = ipw.IntText(description='Grid Points', value=100, step=10)
basis_funcs_label_wgt = ipw.Label('Basis Functions')
basis_funcs_legend_wgt = ipw.VBox([ipw.Label('1=Constant'),
                                   ipw.Label('s=Spot Price'),
                                   ipw.Label('x_st=Short-term Factor'),
                                   ipw.Label('x_sw=Sum/Win Factor'),
                                   ipw.Label('x_lt=Long-term Factor')])

basis_funcs_input_wgt = ipw.Textarea(
    value='1 + x_st + x_sw + x_lt + x_st**2 + x_sw**2 + x_lt**2 + x_st**3 + x_sw**3 + x_lt**3',
    layout=ipw.Layout(width='95%', height='95%'))
basis_func_wgt = ipw.HBox([ipw.VBox([basis_funcs_label_wgt, basis_funcs_legend_wgt]), basis_funcs_input_wgt])
num_tol_wgt = ipw.FloatText(description='Numerical Tol', value=1E-10, step=1E-9)


def on_seed_is_random_change(change):
    random_seed_wgt.disabled = change['new']


seed_is_random_wgt.observe(on_seed_is_random_change, names='value')


def on_fwd_sim_seed_set_change(change):
    fwd_sim_seed_wgt.disabled = not change['new']


fwd_sim_seed_set_wgt.observe(on_fwd_sim_seed_set_change, names='value')

tech_params_wgt = ipw.VBox([tech_params_buttons, ipw.HBox(
    [ipw.VBox([num_sims_wgt, extra_decisions_wgt, seed_is_random_wgt, random_seed_wgt, fwd_sim_seed_set_wgt,
               fwd_sim_seed_wgt, grid_points_wgt, num_tol_wgt]), basis_func_wgt])])


def tech_params_to_dict() -> dict:
    return {'num_sims': num_sims_wgt.value,
            'basis_funcs': basis_funcs_input_wgt.value,
            'seed': random_seed_wgt.value,
            'seed_is_random': seed_is_random_wgt.value,
            'fwd_sim_seed': fwd_sim_seed_wgt.value,
            'set_fwd_sim_seed': fwd_sim_seed_set_wgt.value,
            'extra_decisions': extra_decisions_wgt.value,
            'num_inventory_grid_points': grid_points_wgt.value,
            'numerical_tolerance': num_tol_wgt.value}


# ======================================================================================================
# COMPOSE INPUT TABS

tab_in_titles = ['Valuation Data', 'Forward Curve', 'Storage Details', 'Volatility Params', 'Technical Params']
tab_in_children = [val_inputs_wgt, fwd_data_wgt, storage_details_wgt, vol_params_wgt, tech_params_wgt]
tab_in = create_tab(tab_in_titles, tab_in_children)

# ======================================================================================================
# OUTPUT WIDGETS
progress_wgt = ipw.FloatProgress(min=0.0, max=1.0)
full_value_wgt = ipw.Text(description='Full Value', disabled=True)
intr_value_wgt = ipw.Text(description='Intr. Value', disabled=True)
extr_value_wgt = ipw.Text(description='Extr. Value', disabled=True)
value_wgts = [full_value_wgt, intr_value_wgt, extr_value_wgt]
values_wgt = ipw.VBox(value_wgts)

out_summary = ipw.Output()
summary_vbox = ipw.HBox([values_wgt, out_summary])

sheet_out_layout = {
    'width': '100%',
    'height': '300px',
    'overflow_y': 'auto'}

out_triggers_plot = ipw.Output()

# Buttons to export table results
def create_deltas_dataframe():
    return pd.DataFrame(index=val_results_3f.deltas.index,
                        data={'full_delta': val_results_3f.deltas,
                              'intrinsic_delta': intr_delta})


def create_triggers_dataframe():
    trigger_prices_frame = val_results_3f.trigger_prices.copy()
    trigger_prices_frame['expected_inventory'] = val_results_3f.expected_profile['inventory']
    trigger_prices_frame['fwd_price'] = active_fwd_curve
    return trigger_prices_frame


def on_export_summary_click(b):
    try:
        csv_path = select_file_save('Save table to', 'CSV File (*.csv)', 'storage_deltas.csv')
        if csv_path != '':
            deltas_frame = create_deltas_dataframe()
            deltas_frame.to_csv(csv_path)
    except Exception as e:
        logger.exception(e)


def on_export_triggers_click(b):
    try:
        csv_path = select_file_save('Save table to', 'CSV File (*.csv)', 'trigger_prices.csv')
        if csv_path != '':
            triggers_frame = create_triggers_dataframe()
            triggers_frame.to_csv(csv_path)
    except Exception as e:
        logger.exception(e)


btn_export_summary_wgt = ipw.Button(description='Export Data', disabled=True)
btn_export_summary_wgt.on_click(on_export_summary_click)
btn_export_triggers_wgt = ipw.Button(description='Export Data', disabled=True)
btn_export_triggers_wgt.on_click(on_export_triggers_click)

tab_out_titles = ['Summary', 'Summary Table', 'Trigger Prices Chart', 'Trigger Prices Table']
tab_out_children = [summary_vbox, btn_export_summary_wgt, out_triggers_plot, btn_export_triggers_wgt]
tab_output = create_tab(tab_out_titles, tab_out_children)


def set_tab_output_child(child_index, new_child):
    child_list = list(tab_output.children)
    child_list[child_index] = new_child
    tab_output.children = tuple(child_list)


def on_progress(progress):
    progress_wgt.value = progress


# Inputs Not Defined in GUI
def twentieth_of_next_month(period): return period.asfreq('M').asfreq('D', 'end') + 20


def enumerate_fwd_points():
    fwd_row = 0
    while fwd_input_sheet.cells[0].value[fwd_row] is not None and fwd_input_sheet.cells[0].value[fwd_row] != '':
        fwd_start = fwd_input_sheet.cells[0].value[fwd_row]
        fwd_price = fwd_input_sheet.cells[1].value[fwd_row]
        yield fwd_start, fwd_price
        fwd_row += 1
        if fwd_row == num_fwd_rows:
            break


def read_fwd_curve():
    fwd_periods = []
    fwd_prices = []
    for fwd_start, fwd_price in enumerate_fwd_points():
        fwd_periods.append(pd.Period(fwd_start, freq=freq))
        fwd_prices.append(fwd_price)
    if smooth_curve_wgt.value:
        p1, p2 = itertools.tee(fwd_periods)
        next(p2, None)
        contracts = []
        for start, end, price in zip(p1, p2, fwd_prices):
            contracts.append((start, end - 1, price))
        weekend_adjust = None
        if apply_wkend_shaping_wgt.value:
            wkend_factor = wkend_factor_wgt.value
            weekend_adjust = adjustments.dayofweek(default=1.0, saturday=wkend_factor, sunday=wkend_factor)
        return max_smooth_interp(contracts, freq=freq, mult_season_adjust=weekend_adjust)
    else:
        return pd.Series(fwd_prices, pd.PeriodIndex(fwd_periods)).resample(freq).fillna('pad')


def btn_clicked(b):
    progress_wgt.value = 0.0
    for vw in value_wgts:
        vw.value = ''
    btn_calculate.disabled = True
    out_summary.clear_output()
    out_triggers_plot.clear_output()
    try:
        global fwd_curve
        logger.debug('Reading forward curve.')
        fwd_curve = read_fwd_curve()
        logger.debug('Forward curve read successfully.')
        global storage
        global val_results_3f
        if stor_type_wgt.value == 'Simple':
            storage = CmdtyStorage(freq, storage_start=start_wgt.value, storage_end=end_wgt.value,
                                   injection_cost=inj_cost_wgt.value, withdrawal_cost=with_cost_wgt.value,
                                   min_inventory=invent_min_wgt.value, max_inventory=invent_max_wgt.value,
                                   max_injection_rate=inj_rate_wgt.value, max_withdrawal_rate=with_rate_wgt.value,
                                   cmdty_consumed_inject=inj_consumed_wgt.value,
                                   cmdty_consumed_withdraw=with_consumed_wgt.value)
        else:
            ratchets = read_ratchets()
            storage = CmdtyStorage(freq, storage_start=start_wgt.value, storage_end=end_wgt.value,
                                   injection_cost=inj_cost_wgt.value, withdrawal_cost=with_cost_wgt.value,
                                   ratchets=ratchets, ratchet_interp = RatchetInterp.LINEAR)

        interest_rate_curve = pd.Series(index=pd.period_range(val_date_wgt.value,
                                                              twentieth_of_next_month(
                                                                  pd.Period(end_wgt.value, freq='D')), freq='D'),
                                        dtype='float64')
        interest_rate_curve[:] = ir_wgt.value
        seed = None if seed_is_random_wgt.value else random_seed_wgt.value
        fwd_sim_seed = fwd_sim_seed_wgt.value if fwd_sim_seed_set_wgt.value else None
        logger.info('Valuation started.')
        val_results_3f = three_factor_seasonal_value(storage, val_date_wgt.value, inventory_wgt.value,
                                                     fwd_curve=fwd_curve,
                                                     interest_rates=interest_rate_curve,
                                                     settlement_rule=twentieth_of_next_month,
                                                     spot_mean_reversion=spot_mr_wgt.value,
                                                     spot_vol=spot_vol_wgt.value,
                                                     long_term_vol=lt_vol_wgt.value,
                                                     seasonal_vol=seas_vol_wgt.value,
                                                     num_sims=num_sims_wgt.value,
                                                     basis_funcs=basis_funcs_input_wgt.value,
                                                     discount_deltas=discount_deltas_wgt.value,
                                                     seed=seed, fwd_sim_seed=fwd_sim_seed,
                                                     extra_decisions=extra_decisions_wgt.value,
                                                     num_inventory_grid_points=grid_points_wgt.value,
                                                     on_progress_update=on_progress,
                                                     numerical_tolerance=num_tol_wgt.value)
        logger.info('Valuation completed successfully.')
        full_value_wgt.value = "{0:,.0f}".format(val_results_3f.npv)
        intr_value_wgt.value = "{0:,.0f}".format(val_results_3f.intrinsic_npv)
        extr_value_wgt.value = "{0:,.0f}".format(val_results_3f.extrinsic_npv)
        global intr_delta
        intr_delta = val_results_3f.intrinsic_profile['net_volume']

        btn_export_summary_wgt.disabled = False
        btn_export_triggers_wgt.disabled = False

        global active_fwd_curve
        active_fwd_curve = fwd_curve[storage.start:storage.end]
        with out_summary:
            ax_1 = val_results_3f.deltas.plot(legend=True, label='Full Delta')
            ax_1.set_ylabel('Delta')
            intr_delta.plot(legend=True, ax=ax_1, label='Intrinsic Delta')
            ax_2 = active_fwd_curve.plot(secondary_y=True, legend=True, ax=ax_1, label='Forward Curve')
            h1, l1 = ax_1.get_legend_handles_labels()
            h2, l2 = ax_2.get_legend_handles_labels()
            ax_2.set_ylabel('Forward Price')
            ax_1.legend(h1+h2, l1+l2, loc=0)
            show_inline_matplotlib_plots()
        deltas_frame = create_deltas_dataframe()
        deltas_sheet = dataframe_to_ipysheet(deltas_frame)
        deltas_sheet.layout = sheet_out_layout
        set_tab_output_child(1, ipw.VBox([btn_export_summary_wgt, deltas_sheet]))
        with out_triggers_plot:
            trigger_prices = val_results_3f.trigger_prices
            ax_1 = trigger_prices['inject_trigger_price'].plot(legend=True)
            ax_1.set_ylabel('Price')
            trigger_prices['withdraw_trigger_price'].plot(legend=True, ax=ax_1)
            active_fwd_curve.plot(legend=True, ax=ax_1)
            ax_2 = val_results_3f.expected_profile['inventory'].plot(secondary_y=True, legend=True, ax=ax_1)
            ax_2.set_ylabel('Volume')
            box = ax_1.get_position()
            ax_1.set_position([box.x0, box.y0 + box.height * 0.1, box.width, box.height * 0.9])
            ax_1.legend(['Inject Trigger Price', 'Withdraw Trigger Price', 'Forward Curve'], loc='upper center',
                        bbox_to_anchor=(0.2, -0.12))
            ax_2.legend(['Expected Inventory'], loc='upper center', bbox_to_anchor=(0.7, -0.12))
            show_inline_matplotlib_plots()
        trigger_prices_frame = create_triggers_dataframe()
        triggers_sheet = dataframe_to_ipysheet(trigger_prices_frame)
        triggers_sheet.layout = sheet_out_layout
        set_tab_output_child(3, ipw.VBox([btn_export_triggers_wgt, triggers_sheet]))
    except Exception as e:
        logger.exception(e)
    finally:
        btn_calculate.disabled = False


btn_calculate = ipw.Button(description='Calculate')
btn_calculate.on_click(btn_clicked)

controls_wgt = ipw.HBox([ipw.VBox([btn_calculate, progress_wgt, log_level_wgt, btn_clear_logs]), log_handler.out])


def display_gui():
    display(tab_in)
    display(controls_wgt)
    display(tab_output)


def test_data_btn():
    def btn_test_data_clicked(b):
        today = date.today()
        inventory_wgt.value = 1456
        start_wgt.value = today + timedelta(days=5)
        end_wgt.value = today + timedelta(days=380)
        invent_max_wgt.value = 100000
        wkend_factor_wgt.value = 0.97
        inj_rate_wgt.value = 260
        with_rate_wgt.value = 130
        inj_cost_wgt.value = 1.1
        inj_consumed_wgt.value = 0.01
        with_cost_wgt.value = 1.3
        with_consumed_wgt.value = 0.018
        ir_wgt.value = 0.005
        spot_vol_wgt.value = 1.23
        spot_mr_wgt.value = 14.5
        lt_vol_wgt.value = 0.23
        seas_vol_wgt.value = 0.39
        fwd_prices = [58.89, 61.41, 62.58, 58.9, 43.7, 58.65, 61.45, 56.87]
        fwd_dates = [(today + timedelta(days=do)).strftime('%Y-%m-%d') for do in [0, 30, 60, 90, 150, 250, 350, 400]]
        updated_fwd_input_sheet = create_fwd_input_sheet(fwd_dates, fwd_prices, num_fwd_rows)
        reset_fwd_input_sheet(updated_fwd_input_sheet)
        # Populate ratchets
        dates = [today.strftime('%Y-%m-%d'), '', '', '', ''] + \
                [(today + timedelta(days=150)).strftime('%Y-%m-%d'), '', '', '', '']
        inventories = [0.0, 25000.0, 50000.0, 60000.0, 65000.0] + [0.0, 24000.0, 48000.0, 61000.0, 65000.0]
        inject_rates = [650.0, 552.5, 512.8, 498.6, 480.0] + [645.8, 593.65, 568.55, 560.8, 550.0]
        withdraw_rates = [702.7, 785.0, 790.6, 825.6, 850.4] + [752.5, 813.7, 836.45, 854.78, 872.9]
        new_ratchets_input = create_ratchets_sheet(dates, inventories, inject_rates, withdraw_rates, num_ratch_rows)
        reset_ratchets_sheet(new_ratchets_input)
        storage_details_wgt.children = (storage_common_wgt, storage_simple_wgt)

    btn_test_data = ipw.Button(description='Populate Test Data')
    btn_test_data.on_click(btn_test_data_clicked)

    display(btn_test_data)
