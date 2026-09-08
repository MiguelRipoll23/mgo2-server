/* containing function of static patch target(s); entry .opd.FUN_000e4ea8 @ 000e4ea8 */

undefined4 _opd_FUN_000e4ea8(void)

{
  int iVar1;
  
  iVar1 = *(int *)(PTR_PTR_0121ace8 + -0x7ffc);
  syscall(0);
  *(int *)(iVar1 + 0x7e0) = *(int *)(iVar1 + 0x7e0) + 1;
  syscall(0);
  syscall(0);
  return *(undefined4 *)(iVar1 + 0x100);
}

