// original call TARGET 0x00effa0c

undefined8 _opd_FUN_00effa0c(int param_1,int param_2,undefined4 param_3)

{
  char cVar1;
  undefined4 *puVar2;
  
  if (param_1 != 0) {
    if (param_2 - 1U < 0x47) {
      puVar2 = *(undefined4 **)(param_1 + param_2 * 4 + 0x28b58);
      if (puVar2 != (undefined4 *)0x0) {
        (*(code *)*puVar2)(param_2,param_3);
      }
      param_1 = param_1 + param_2 + 0x28c70;
      cVar1 = *(char *)(param_1 + 8);
      if (cVar1 != '\x7f') {
        *(char *)(param_1 + 8) = cVar1 + '\x01';
      }
    }
  }
  return 0;
}

