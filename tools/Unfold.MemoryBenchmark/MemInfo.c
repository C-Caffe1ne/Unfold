#include <mach/mach.h>
#include <mach/task_info.h>
#include <stdint.h>
int unfold_memory(uint64_t *out) {
    for (int index = 0; index < 16; index++) out[index] = 0;
    task_vm_info_data_t info = {0};
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    kern_return_t result = task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&info, &count);
    if (result != KERN_SUCCESS) return result;
    if (count < TASK_VM_INFO_REV3_COUNT) return KERN_NOT_SUPPORTED;
    out[0] = info.resident_size;
    out[1] = info.resident_size_peak;
    out[2] = info.phys_footprint;
    out[3] = info.ledger_phys_footprint_peak;
    out[4] = info.internal;
    out[5] = info.compressed;
    out[6] = info.ledger_tag_graphics_footprint;
    out[7] = info.ledger_tag_graphics_footprint_compressed;
    out[8] = info.ledger_purgeable_nonvolatile;
    out[9] = info.ledger_purgeable_novolatile_compressed;
    out[10] = info.device;
    out[11] = info.device_peak;
    out[12] = info.reusable;
    out[13] = info.external;
    out[14] = info.ledger_tag_graphics_nofootprint;
    out[15] = info.ledger_tag_graphics_nofootprint_compressed;
    return 0;
}
