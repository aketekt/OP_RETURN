using System;
using System.Net;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;


namespace OpReturn
{
    class DataHasher
    {
        public Transaction CreateOpReturnTx(RPCClient rpc, string sourceTxId, int outputIndex, BitcoinAddress sourceAddress, BitcoinSecret sourcePrivateKey, string message)
        {
            // Can either use the raw transaction hex from a wallet's getrawtransaction CLI command, or look up the equivalent information via RPC
            Transaction tx = rpc.GetRawTransaction(uint256.Parse(sourceTxId));

            // For the fee to be correctly calculated, the quantity of funds in the source transaction needs to be known
            Money remainingBalance = tx.Outputs[outputIndex].Value;

            // An outpoint needs to be created from the correct output of the source transaction
            OutPoint outPoint = new OutPoint(tx, outputIndex);

            // Use the previous transaction's output (must be unspent) as the input for the new OP_RETURN transaction
            Transaction sendTx = new Transaction();
            sendTx.Inputs.Add(new TxIn()
            {
                PrevOut = outPoint
            });

            // Send the change back to the originating address. The Value amount needs to be the value of the unspent source output minus 2x 0.0001 transaction fees (i.e. 0.0002)
            TxOut changeBackTxOut = new TxOut()
            {
                Value = new Money(((remainingBalance.ToDecimal(MoneyUnit.BTC) - (decimal)0.0002)), MoneyUnit.BTC),
                ScriptPubKey = sourceAddress.ScriptPubKey
            };

            sendTx.Outputs.Add(changeBackTxOut);

            // Can currently only send a maximum of 40 bytes in the null data transaction.
            // Also note that a nulldata transaction currently has to have a nonzero value assigned to it for older Stratis nodes to accept it. This is expected to be corrected in future.
            byte[] bytes = Encoding.UTF8.GetBytes(message);

            sendTx.Outputs.Add(new TxOut()
            {
                Value = new Money((decimal)0.0001, MoneyUnit.BTC),
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)          
            });

            sendTx.Inputs[0].ScriptSig = sourceAddress.ScriptPubKey;
            sendTx.Sign(sourcePrivateKey,false);

            Console.WriteLine(sendTx);

            //Broadcast Tx to network
            try
            {          
                rpc.SendRawTransaction(sendTx);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }

            return sendTx;
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            //Connect to local node / Wallet-QT
            NetworkCredential credentials = new NetworkCredential("user", "password");
            RPCClient rpc = new RPCClient(credentials, new Uri("http://127.0.0.1:26174/"), Network.StratisTest);

            Key privateKey = Key.Parse("VgEhPfQ5b9id96xiQGEcq6SQxH2rBS93LfpNoyyH2UTTadR4CHCE"); //The Private Key
            BitcoinSecret testNetPrivateKey = privateKey.GetBitcoinSecret(Network.StratisTest);
            BitcoinPubKeyAddress testNetAddress = privateKey.PubKey.GetAddress(Network.StratisTest); //Get Address from Private Key
           
            var UnspentTx = "f64e8834fcd98bcac1d5a0e4f139d90c12d92b7fa8123c405d63da3a5c4011e9"; //Last Transaction (unspent Transaction)
            var outputs = 0;
            var dataToSave = "Testing Data"; // The String you want in OP_RETURN

            Console.WriteLine(testNetAddress);

            DataHasher dataHasher = new DataHasher();
            dataHasher.CreateOpReturnTx(rpc, UnspentTx, outputs, testNetAddress, testNetPrivateKey, dataToSave);
           
            Console.ReadLine();
        }
    }
}