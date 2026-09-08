/* containing function of static patch target(s); entry .opd.FUN_002871b8 @ 002871b8 */

undefined4 _opd_FUN_002871b8(int param_1,int param_2)

{
  int iVar1;
  undefined4 uVar2;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  uint uStack00000008;
  undefined1 auStack_40 [24];
  
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  iVar1 = *(int *)(param_1 + 0x1018);
  if (iVar1 == param_2) {
    do {
      *(uint *)(param_1 + 0x1014) = *(uint *)(param_1 + 0x1014) | 1;
      syscall(0);
      syscall(0);
    } while ((int)*(undefined8 *)(param_1 + 0x1020) != 0);
    *(undefined4 *)(param_1 + 0x1018) = 0xffffffff;
    FUN_00fbd87c(param_1 + 0x1030,auStack_40);
    syscall(0);
    uVar2 = (*(code *)**(undefined4 **)(**(int **)(param_1 + 4) + 4))(*(int **)(param_1 + 4),iVar1);
  }
  else {
    (*(code *)**(undefined4 **)(**(int **)(param_1 + 4) + 4))(*(int **)(param_1 + 4),param_2);
    uVar2 = 0xffffffff;
  }
  return uVar2;
}

